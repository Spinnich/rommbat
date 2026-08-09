using System.Net;
using System.Net.Sockets;

namespace RomM.Client;

/// <summary>Base type for every failure this client raises deliberately.</summary>
public abstract class RomMException : Exception
{
    protected RomMException(string message)
        : base(message)
    {
    }

    protected RomMException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Why the server could not be reached.</summary>
public enum UnreachableReason
{
    /// <summary>The TCP handshake did not complete inside <see cref="RomMClientOptions.ConnectTimeout"/>.</summary>
    ConnectTimeout,

    /// <summary>The hostname did not resolve.</summary>
    NameResolution,

    /// <summary>Something answered and refused the connection.</summary>
    ConnectionRefused,

    /// <summary>The TLS handshake failed.</summary>
    Tls,

    /// <summary>The request began but did not finish inside <see cref="RomMClientOptions.RequestTimeout"/>.</summary>
    RequestTimeout,

    /// <summary>Anything else at the network layer.</summary>
    Network,
}

/// <summary>
/// The server was not reachable. Being offline is the normal case, not an error path, so
/// callers are expected to catch this and fall back to local state.
/// </summary>
/// <remarks>
/// M0 probe 6b measured that an unreachable host on the local subnet and a user
/// cancellation both surface as <see cref="TaskCanceledException"/>, differing only in the
/// inner exception. Everything that talks to the server routes its failures through
/// <see cref="RomMTransportErrors.Classify"/> so the two never get confused.
/// </remarks>
public sealed class RomMUnreachableException : RomMException
{
    public RomMUnreachableException(UnreachableReason reason, string message, Exception? innerException = null)
        : base(message, innerException) => Reason = reason;

    public UnreachableReason Reason { get; }
}

/// <summary>The server answered, and the answer was an error status.</summary>
public sealed class RomMApiException : RomMException
{
    public RomMApiException(HttpStatusCode statusCode, string? detail, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Detail = detail;
    }

    public HttpStatusCode StatusCode { get; }

    /// <summary>The <c>detail</c> field of RomM's error body, when it carried one.</summary>
    public string? Detail { get; }
}

/// <summary>
/// Turns transport-layer exceptions into ones a caller can act on.
/// </summary>
public static class RomMTransportErrors
{
    /// <summary>
    /// Classifies an exception thrown by <see cref="HttpClient"/>.
    /// </summary>
    /// <remarks>
    /// The order matters. A caller's own cancellation is checked first, because a
    /// <see cref="TaskCanceledException"/> from a real cancellation and one from a connect
    /// timeout are the same type. Anything unrecognised is returned unchanged so it
    /// surfaces rather than being mislabelled as an offline server.
    /// </remarks>
    /// <param name="exception">What <see cref="HttpClient"/> threw.</param>
    /// <param name="requestUri">Used only to build the message.</param>
    /// <param name="cancellationToken">The token the caller passed in.</param>
    public static Exception Classify(Exception exception, Uri? requestUri, CancellationToken cancellationToken)
    {
        var target = requestUri is null ? "the server" : requestUri.GetLeftPart(UriPartial.Authority);

        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return exception;
        }

        if (exception is TaskCanceledException canceled)
        {
            // Inner TimeoutException is how SocketsHttpHandler reports a ConnectTimeout, and
            // how HttpClient reports its own Timeout. Both mean "not reachable in budget".
            var reason = canceled.InnerException is TimeoutException
                ? UnreachableReason.ConnectTimeout
                : UnreachableReason.RequestTimeout;

            return new RomMUnreachableException(
                reason,
                $"{target} did not answer in time.",
                exception);
        }

        if (exception is HttpRequestException request)
        {
            var reason = request.InnerException switch
            {
                SocketException { SocketErrorCode: SocketError.HostNotFound or SocketError.NoData } =>
                    UnreachableReason.NameResolution,
                SocketException { SocketErrorCode: SocketError.ConnectionRefused } =>
                    UnreachableReason.ConnectionRefused,
                SocketException { SocketErrorCode: SocketError.TimedOut } =>
                    UnreachableReason.ConnectTimeout,
                System.Security.Authentication.AuthenticationException => UnreachableReason.Tls,
                _ => UnreachableReason.Network,
            };

            var detail = reason switch
            {
                UnreachableReason.NameResolution => $"{target} does not resolve to an address.",
                UnreachableReason.ConnectionRefused => $"{target} refused the connection.",
                UnreachableReason.Tls => $"The TLS handshake with {target} failed.",
                UnreachableReason.ConnectTimeout => $"{target} did not answer in time.",
                _ => $"{target} could not be reached.",
            };

            return new RomMUnreachableException(reason, detail, exception);
        }

        return exception;
    }
}
