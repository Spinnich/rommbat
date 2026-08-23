using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using RomM.Client;
using RomM.Client.Catalog;
using RomM.Client.Content;
using RomMBat.Core.Content;
using RomMBat.Core.Mapping;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The download path against a real RomM, which is where M3's measurements came from.
/// </summary>
/// <remarks>
/// Skipped unless <c>ROMMBAT_TEST_SERVER</c> and <c>ROMMBAT_TEST_APPROVER_TOKEN</c> are set, so
/// a clone with no server still runs green. Nothing here names an instance.
/// <para>
/// Every test picks the smallest ROM that answers its question and fetches nothing larger than a
/// few megabytes: this suite runs against someone's real library, and being polite to it is part
/// of the contract. Everything is a read.
/// </para>
/// <para>
/// These are the findings that reshaped the milestone, kept as tests rather than as prose so a
/// server that changes its mind is noticed here instead of in the field.
/// </para>
/// </remarks>
public class LiveContentTests(LiveCatalogFixture fixture) : IClassFixture<LiveCatalogFixture>
{
    private const string PageFlags =
        "with_char_index=false&with_rom_id_index=false&with_filter_values=false&with_total=true&with_files=false";

    private const string NotConfigured =
        "Set ROMMBAT_TEST_SERVER and ROMMBAT_TEST_APPROVER_TOKEN to run the live tests.";

    private static bool IsConfigured => LiveCatalogFixture.IsConfigured;

    [Fact]
    public async Task A_single_file_rom_resumes_into_a_byte_identical_file()
    {
        Assert.SkipUnless(IsConfigured, NotConfigured);

        var rom = await SmallSingleFileAsync();
        Assert.SkipWhen(rom is null, "No small single-file ROM on this instance.");

        var connection = fixture.Session.Connection;
        var request = new RomContentRequest { RomId = rom!.Id, FsName = rom.FsName };

        await using var whole = new MemoryStream();
        var full = await connection.DownloadRomContentAsync(
            request,
            whole,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(full.IsSuccess, full.Message);
        Assert.Equal(rom.SizeBytes, whole.Length);

        // fs_size_bytes is what the budget is arithmetic on, so it agreeing with the bytes that
        // actually arrive is load-bearing rather than incidental.
        Assert.Equal(rom.SizeBytes, full.Value!.TotalBytes);
        Assert.False(string.IsNullOrWhiteSpace(full.Value.Validator));

        var cut = (int)(whole.Length / 2);
        await using var spliced = new MemoryStream();
        spliced.Write(whole.ToArray().AsSpan(0, cut));

        var resumed = await connection.DownloadRomContentAsync(
            request with { ResumeFrom = cut, Validator = full.Value.Validator },
            spliced,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(resumed.IsSuccess, resumed.Message);
        Assert.True(resumed.Value!.Resumed, "A range request should have been honoured with a 206.");
        Assert.False(resumed.Value.RestartedFromScratch);
        Assert.Equal(whole.ToArray(), spliced.ToArray());
    }

    [Fact]
    public async Task A_stale_validator_sends_the_whole_file_rather_than_splicing()
    {
        Assert.SkipUnless(IsConfigured, NotConfigured);

        var rom = await SmallSingleFileAsync();
        Assert.SkipWhen(rom is null, "No small single-file ROM on this instance.");

        var connection = fixture.Session.Connection;
        var request = new RomContentRequest { RomId = rom!.Id, FsName = rom.FsName };

        await using var whole = new MemoryStream();
        var fetched = await connection.DownloadRomContentAsync(
            request,
            whole,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(fetched.IsSuccess);

        await using var partial = new MemoryStream();
        partial.Write(whole.ToArray().AsSpan(0, (int)(whole.Length / 2)));

        // The worst thing this milestone could produce is a silent splice onto bytes that no
        // longer describe anything. The server's answer is a full 200, and the client turns
        // that into a restart rather than a hybrid file.
        var response = await connection.DownloadRomContentAsync(
            request with { ResumeFrom = partial.Length, Validator = "\"rommbat-stale-validator\"" },
            partial,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccess, response.Message);
        Assert.True(response.Value!.RestartedFromScratch);
        Assert.Equal(whole.ToArray(), partial.ToArray());
    }

    [Fact]
    public async Task A_range_on_a_multi_file_rom_is_refused_which_is_why_none_is_sent()
    {
        Assert.SkipUnless(IsConfigured, NotConfigured);

        var rom = await MultiFileAsync();
        Assert.SkipWhen(rom is null, "No multi-file ROM on this instance.");

        // Deliberately lying about the ROM's shape, to prove the header is what breaks it. The
        // shipped path never gets here: a multi-file ROM is excluded when the set resolves.
        await using var destination = new MemoryStream();
        var response = await fixture.Session.Connection.DownloadRomContentAsync(
            new RomContentRequest { RomId = rom!.Id, FsName = rom.FsName, IsMultiFile = false },
            destination,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(response.IsSuccess);
        Assert.Equal(RomMResponseStatus.Forbidden, response.Status);
    }

    [Fact]
    public async Task The_reported_hashes_describe_the_uncompressed_content()
    {
        Assert.SkipUnless(IsConfigured, NotConfigured);

        var rom = await SmallSingleFileAsync(extension: "zip");
        Assert.SkipWhen(rom is null, "No small zipped ROM on this instance.");

        await using var buffer = new MemoryStream();
        var response = await fixture.Session.Connection.DownloadRomContentAsync(
            new RomContentRequest { RomId = rom!.Id, FsName = rom.FsName },
            buffer,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccess, response.Message);

        var bytes = buffer.ToArray();
        Assert.SkipWhen(bytes.Length < 4 || bytes[0] != 0x50 || bytes[1] != 0x4B, "The server did not send a zip.");

        using var archive = new ZipArchive(new MemoryStream(bytes));
        var entries = archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)).ToList();
        Assert.SkipWhen(entries.Count != 1, "Only a single-entry archive can carry one content hash.");

        await using var inner = entries[0].Open();
        await using var content = new MemoryStream();
        await inner.CopyToAsync(content, TestContext.Current.CancellationToken);

        // The measurement that reshaped verification: md5 and sha1 describe what is inside the
        // archive, not the archive. Hashing the bytes that arrived would fail every zipped ROM.
        Assert.Equal(Md5(content.ToArray()), rom.Md5Hash, ignoreCase: true);
        Assert.NotEqual(Md5(bytes), rom.Md5Hash, StringComparer.OrdinalIgnoreCase);

        // And the shipped hasher agrees with the server about which of the two it means.
        var temporary = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".part");
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, TestContext.Current.CancellationToken);
            var fingerprint = ContentHasher.Compute(temporary, rom.FsName);

            Assert.Equal(HashScope.ArchiveContent, fingerprint.Scope);
            Assert.Equal(rom.Md5Hash, fingerprint.Md5, ignoreCase: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public async Task The_paged_read_carries_the_hashes_and_the_multi_file_flag()
    {
        Assert.SkipUnless(IsConfigured, NotConfigured);

        var page = await fixture.Session.Connection.GetRomPageAsync(
            new CatalogQuery { Scope = CatalogScopeKind.Filter },
            limit: 100,
            offset: 0,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.SkipWhen(page.Status == RomMResponseStatus.Forbidden, "This account cannot read the library.");
        Assert.True(page.IsSuccess, page.Message);
        Assert.SkipWhen(page.Value!.Items.Count == 0, "The library is empty.");

        // Adoption and verification both need a hash on the membership row, and getting one
        // without a per-ROM call is what keeps a 40-game set to 40 requests.
        Assert.Contains(page.Value.Items, row => !string.IsNullOrWhiteSpace(row.Md5Hash));

        // Measured at 105 of 105 both ways: the flag and the empty extension travel together,
        // which is why M2's extension filter already excludes every multi-file ROM.
        Assert.All(
            page.Value.Items.Where(row => row.HasMultipleFiles),
            row => Assert.True(string.IsNullOrWhiteSpace(row.FsExtension)));
    }

    [Fact]
    public async Task The_identifiers_endpoint_is_asked_under_a_budget_and_never_throws()
    {
        Assert.SkipUnless(IsConfigured, NotConfigured);

        // Measured at 504 after 300 s on an 83k library, which is why deletion is reconciled
        // through set re-resolution. It stays a fast-path cross-check, so what matters is that
        // it answers inside its budget either way.
        var started = System.Diagnostics.Stopwatch.StartNew();
        var identifiers = await fixture.Session.Connection.TryGetRomIdentifiersAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);
        started.Stop();

        Assert.True(
            started.Elapsed < TimeSpan.FromSeconds(30),
            $"The budget did not bound the call: it took {started.Elapsed.TotalSeconds:0} s.");

        if (identifiers is not null)
        {
            Assert.NotEmpty(identifiers);
        }
    }

    [Fact]
    public async Task A_real_set_syncs_to_completion_and_the_second_run_is_a_no_op()
    {
        Assert.SkipUnless(IsConfigured, NotConfigured);

        var session = fixture.Session;
        var systems = Fixtures.LoadEsSystems();
        var resolver = new PlatformResolver(systems);

        var platforms = await session.Connection.ListPlatformsAsync(
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.SkipWhen(platforms.Status == RomMResponseStatus.Forbidden, "This account cannot read platforms.");
        Assert.True(platforms.IsSuccess, platforms.Message);

        var candidate = platforms.Value!
            .Where(platform => platform.RomCount > 0)
            .FirstOrDefault(platform => resolver
                .Resolve(new RomMPlatform(platform.Id, platform.Slug, platform.FsSlug, platform.Label))
                .IsApplied);

        Assert.SkipWhen(candidate is null, "No platform on this instance both has ROMs and maps to a folder.");

        // Two games, smallest first. This runs against someone's real library, so the set is
        // deliberately the smallest thing that can still prove the milestone's done-when.
        var set = session.Store.SyncSets.Add(
            new SyncSetDefinition
            {
                Name = "live content",
                Scope = CatalogScopeKind.Platform,
                ScopeValue = candidate!.Id.ToString(CultureInfo.InvariantCulture),
                MaxGames = 2,
                Ordering = SetOrdering.SizeAscending,
            },
            DateTimeOffset.UtcNow);

        var resolution = await new SetResolver(systems, resolver).ResolveAsync(
            set,
            new RomPager(session.Connection, SetResolver.QueryFor(set)),
            DateTimeOffset.UtcNow,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(ResolutionOutcome.Resolved, resolution.Outcome);
        Assert.SkipWhen(resolution.Members.Count == 0, "The chosen platform resolved to nothing syncable.");

        session.Store.SyncSets.ReplaceMembers(
            set.Id,
            [.. resolution.Members, .. resolution.Excluded],
            resolution.Summary,
            DateTimeOffset.UtcNow);

        var members = session.Store.SyncSets.Members(set.Id);
        var planner = new ContentPlanner(session.Install, session.Store);
        var outcome = await new ContentSync(session.Install, session.Store, session.Connection)
            .ApplyAsync(planner.Plan(set, members), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, outcome.Failed);
        Assert.Equal(members.Count, outcome.Downloaded);

        foreach (var member in members)
        {
            var path = session.Install.Resolve(ContentPlanner.TargetFor(member));
            Assert.True(File.Exists(path), $"{member.FsName} did not land on disk.");
            Assert.Equal(member.SizeBytes, new FileInfo(path).Length);
        }

        // Every file verified against a hash the server gave us, not merely against its length.
        Assert.All(
            session.Store.Files.List(),
            file => Assert.True(
                file.VerifiedBy is VerifiedBy.Md5 or VerifiedBy.Sha1,
                $"{file.FileName} was only verified by {file.VerifiedBy}."));

        // The milestone's done-when: a second run writes nothing and says so.
        var second = planner.Plan(set, session.Store.SyncSets.Members(set.Id));

        Assert.True(second.IsNoOp);
        Assert.Contains("nothing to do", second.Summary, StringComparison.Ordinal);
    }

    /// <summary>The smallest single-file ROM that carries an md5, optionally of one format.</summary>
    private async Task<RomRow?> SmallSingleFileAsync(string? extension = null)
    {
        for (var offset = 0; offset < 1500; offset += 250)
        {
            var page = await fixture.Session.Connection.GetRomPageAsync(
                new CatalogQuery { Scope = CatalogScopeKind.Filter, OrderBy = "fs_size_bytes" },
                limit: 250,
                offset: offset);

            if (!page.IsSuccess)
            {
                return null;
            }

            var match = page.Value!.Items.FirstOrDefault(row =>
                !row.HasMultipleFiles
                && row.SizeBytes is > 1024 and < 4 * 1024 * 1024
                && !string.IsNullOrWhiteSpace(row.Md5Hash)
                && (extension is null || string.Equals(row.FsExtension, extension, StringComparison.OrdinalIgnoreCase)));

            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>Any multi-file ROM, found on a disc-based platform rather than by scanning.</summary>
    /// <remarks>
    /// They are all disc images, so a size-ordered scan from the bottom of the library never
    /// reaches one.
    /// </remarks>
    private async Task<RomRow?> MultiFileAsync()
    {
        var platforms = await fixture.Session.Connection.ListPlatformsAsync();
        if (!platforms.IsSuccess)
        {
            return null;
        }

        string[] discBased = ["segacd", "psx", "saturn", "pcenginecd", "3do", "dreamcast", "neogeocd"];

        foreach (var platform in platforms.Value!.Where(platform =>
            discBased.Contains(platform.FsSlug ?? platform.Slug, StringComparer.OrdinalIgnoreCase)))
        {
            var page = await fixture.Session.Connection.GetRomPageAsync(
                new CatalogQuery
                {
                    Scope = CatalogScopeKind.Platform,
                    ScopeId = platform.Id.ToString(CultureInfo.InvariantCulture),
                    OrderBy = "fs_size_bytes",
                },
                limit: 100,
                offset: 0);

            if (page.IsSuccess && page.Value!.Items.FirstOrDefault(row => row.HasMultipleFiles) is { } found)
            {
                return found;
            }
        }

        return null;
    }

#pragma warning disable CA5351 // Mirrors what RomM stores; not a security primitive.
    private static string Md5(byte[] content) => Convert.ToHexString(MD5.HashData(content)).ToLowerInvariant();
#pragma warning restore CA5351
}
