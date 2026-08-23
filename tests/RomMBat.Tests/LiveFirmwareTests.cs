using RomM.Client.Catalog;
using RomMBat.Core.Content;
using RomMBat.Core.RetroBat;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The BIOS join against a real RomM, which is where M5's measurements came from.
/// </summary>
/// <remarks>
/// Skipped unless <c>ROMMBAT_TEST_SERVER</c> and <c>ROMMBAT_TEST_APPROVER_TOKEN</c> are set, so
/// a clone with no server still runs green. Nothing here names an instance, and nothing asserts
/// a count that belongs to one library: what this suite pins is shape, which is what would
/// break if the server changed its mind.
/// <para>
/// The credential is a real device token minted by the shared pairing fixture, so these also
/// answer whether a device token reaches the firmware routes at all.
/// </para>
/// </remarks>
public class LiveFirmwareTests(LiveCatalogFixture fixture) : IClassFixture<LiveCatalogFixture>
{
    private const string NotConfigured =
        "Set ROMMBAT_TEST_SERVER and ROMMBAT_TEST_APPROVER_TOKEN to run the live tests.";

    private static bool IsConfigured => LiveCatalogFixture.IsConfigured;

    [Fact]
    public async Task The_platform_list_inlines_every_firmware_record_with_its_md5()
    {
        Assert.SkipUnless(IsConfigured, NotConfigured);

        var platforms = await PlatformsAsync(cancellationToken: TestContext.Current.CancellationToken);
        var records = platforms.SelectMany(platform => platform.Firmware).ToList();

        Assert.SkipWhen(records.Count == 0, "This instance holds no firmware.");

        // The claim M5's whole shape rests on: one request, not one per platform. Complete
        // rather than a preview, and every record carries the only field the join reads.
        Assert.All(platforms, platform => Assert.Equal(platform.FirmwareCount, platform.Firmware.Count));
        Assert.All(records, record => Assert.False(string.IsNullOrWhiteSpace(record.Md5Hash)));
        Assert.All(records, record => Assert.True(record.SizeBytes >= 0));
    }

    [Fact]
    public async Task The_inlined_array_holds_the_same_records_the_dedicated_call_does()
    {
        Assert.SkipUnless(IsConfigured, NotConfigured);

        var platforms = await PlatformsAsync(cancellationToken: TestContext.Current.CancellationToken);
        var widest = platforms.MaxBy(platform => platform.Firmware.Count);

        Assert.SkipWhen(widest is null || widest.Firmware.Count == 0, "This instance holds no firmware.");

        var dedicated = await fixture.Session.Connection.ListFirmwareAsync(
            widest!.Id,
            TestContext.Current.CancellationToken);

        Assert.True(dedicated.IsSuccess, dedicated.Message);
        Assert.Equal(
            widest.Firmware.Select(record => record.Id).Order(),
            dedicated.Value!.Select(record => record.Id).Order());
    }

    [Fact]
    public async Task A_required_bios_is_found_by_md5_alone_and_arrives_as_the_bytes_RetroBat_wants()
    {
        Assert.SkipUnless(IsConfigured, NotConfigured);

        var manifest = BiosManifest.Bundled;
        var candidates = BiosPlanner.IndexByMd5(await PlatformsAsync(cancellationToken: TestContext.Current.CancellationToken));

        // The smallest file this library holds that RetroBat actually requires, so a live run
        // against someone's real instance moves as few bytes as it can.
        var match = manifest.Requirements
            .Where(requirement => requirement.Md5 is not null)
            .Select(requirement => (requirement, row: candidates.GetValueOrDefault(requirement.Md5!)))
            .Where(pair => pair.row is { } row && row.IsFetchable)
            .OrderBy(pair => pair.row!.SizeBytes)
            .FirstOrDefault();

        Assert.SkipWhen(match.row is null, "This instance holds none of the firmware RetroBat requires.");

        await using var buffer = new MemoryStream();
        var response = await fixture.Session.Connection.DownloadFirmwareAsync(
            match.row!,
            buffer,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccess, response.Message);
        Assert.Equal(match.row!.SizeBytes, buffer.Length);

        // Written to a file so the check runs through the same code the sync does, and against
        // RetroBat's hash rather than RomM's.
        var path = Path.Combine(fixture.Session.Install.RootPath, "bios", match.requirement.FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, buffer.ToArray(), TestContext.Current.CancellationToken);

        Assert.Equal(match.requirement.Md5, ContentHasher.ComputeFileMd5(path));

        // And the name it was served under is not the name it was stored under, more often than
        // not. This is the join the plan forbids, demonstrated rather than argued.
        Assert.False(string.IsNullOrWhiteSpace(match.row.FileName));
    }

    [Fact]
    public async Task A_record_whose_file_the_server_lost_fails_with_something_a_person_can_act_on()
    {
        Assert.SkipUnless(IsConfigured, NotConfigured);

        var platforms = await PlatformsAsync(cancellationToken: TestContext.Current.CancellationToken);
        var lost = platforms.SelectMany(platform => platform.Firmware).FirstOrDefault(record => record.MissingFromFs);

        Assert.SkipWhen(lost is null, "This instance has no firmware row whose file is gone.");

        await using var buffer = new MemoryStream();
        var response = await fixture.Session.Connection.DownloadFirmwareAsync(
            lost!,
            buffer,
            cancellationToken: TestContext.Current.CancellationToken);

        // 500 rather than 404, measured, so the message has to say what it actually means or a
        // user goes looking through the server's logs for a fault that is not there.
        Assert.False(response.IsSuccess);
        Assert.Contains("no longer has the file", response.Message, StringComparison.Ordinal);
        Assert.Contains(lost!.FileName, response.Message, StringComparison.Ordinal);
    }

    private async Task<IReadOnlyList<PlatformRow>> PlatformsAsync(CancellationToken cancellationToken = default)
    {
        var response = await fixture.Session.Connection
            .ListPlatformsAsync(cancellationToken: cancellationToken);

        Assert.True(response.IsSuccess, response.Message);
        return response.Value!;
    }
}
