using System.Diagnostics;
using System.Net;
using RomM.Client;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// A body that stops arriving ends the call rather than hanging it, streamed or JSON.
/// </summary>
/// <remarks>
/// Under <see cref="HttpCompletionOption.ResponseHeadersRead"/>, once the headers arrive
/// <see cref="HttpClient.Timeout"/> no longer covers the body. Measured on a real socket
/// (finding 267): a 2 s timeout and a body that stopped after 100 bytes was still reading at 8 s.
/// Every call was sent that way, so a restore, or a background flush nobody watches, hung until
/// someone cancelled it (#198).
/// </remarks>
public class BodyStallTests
{
    private static readonly TimeSpan Stall = TimeSpan.FromMilliseconds(300);

    public static TheoryData<string> Routes => ["save", "state", "screenshot"];

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_body_that_stops_arriving_is_unreachable_within_the_stall_window(string route)
    {
        using var handler = new StallingHandler();
        using var connection = Connect(handler, Stall);
        using var destination = new MemoryStream();

        var clock = Stopwatch.StartNew();
        var thrown = await Assert.ThrowsAsync<RomMUnreachableException>(
            () => DownloadAsync(connection, route, destination, TestContext.Current.CancellationToken));

        Assert.Equal(UnreachableReason.RequestTimeout, thrown.Reason);
        Assert.Equal(StallingHandler.Head.Length, destination.Length);

        // Generous against a loaded build agent, and still far short of forever.
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"took {clock.Elapsed}");
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Cancelling_a_stalled_body_stays_a_cancellation(string route)
    {
        using var handler = new StallingHandler();

        // A window no test waits out, so only the caller's token can end the read.
        using var connection = Connect(handler, TimeSpan.FromMinutes(5));
        using var destination = new MemoryStream();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancel.CancelAfter(Stall);

        var thrown = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => DownloadAsync(connection, route, destination, cancel.Token));

        Assert.IsNotType<RomMUnreachableException>(thrown);
    }

    [Fact]
    public async Task A_json_body_that_stops_arriving_is_unreachable_within_the_request_timeout()
    {
        // The same hole on every API call: the heartbeat, a negotiate, a page of ROMs. The body
        // is buffered inside the send now, so the request timeout covers it.
        using var handler = new StallingHandler();
        using var connection = new RomMConnection(
            new RomMClientOptions { Origin = new Uri("https://romm.invalid"), RequestTimeout = Stall },
            handler);

        var clock = Stopwatch.StartNew();
        var thrown = await Assert.ThrowsAsync<RomMUnreachableException>(
            () => connection.ProbeAsync(TestContext.Current.CancellationToken));

        Assert.Equal(UnreachableReason.RequestTimeout, thrown.Reason);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"took {clock.Elapsed}");
    }

    [Fact]
    public async Task An_error_body_that_stops_arriving_costs_the_detail_and_not_the_call()
    {
        // A streamed download reads its error body past the headers, where no timeout reaches.
        // The detail only improves the message, so it is dropped rather than waited for.
        using var handler = new StallingHandler(HttpStatusCode.InternalServerError);
        using var connection = Connect(handler, Stall);
        using var destination = new MemoryStream();

        var response = await connection.DownloadStateAsync(1, destination, TestContext.Current.CancellationToken);

        Assert.False(response.IsSuccess);
        Assert.Equal(RomMResponseStatus.ServerError, response.Status);
    }

    private static RomMConnection Connect(StallingHandler handler, TimeSpan stall) =>
        new(
            new RomMClientOptions
            {
                Origin = new Uri("https://romm.invalid"),
                AccessToken = "rmm_test",
                StallTimeout = stall,
            },
            handler);

    private static async Task DownloadAsync(
        RomMConnection connection,
        string route,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var response = route switch
        {
            "save" => await connection.DownloadSaveAsync(1, "device", null, destination, cancellationToken),
            "state" => await connection.DownloadStateAsync(1, destination, cancellationToken),
            _ => await connection.DownloadScreenshotAsync(1, destination, cancellationToken),
        };

        Assert.Fail($"The download returned {response.Status} instead of ending on the stall.");
    }

    /// <summary>Answers its status, sends the start of a body, then holds the connection open silently.</summary>
    private sealed class StallingHandler(HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public static readonly byte[] Head = [1, 2, 3, 4, 5, 6, 7, 8];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StreamContent(new StallingStream(Head)),
            });
    }

    private sealed class StallingStream(byte[] head) : Stream
    {
        private bool _sent;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_sent)
            {
                _sent = true;
                head.CopyTo(buffer);
                return head.Length;
            }

            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
