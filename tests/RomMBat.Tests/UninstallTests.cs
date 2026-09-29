using RomMBat.Core;
using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Taking the tree back to its state before RomMBat, and the one rule that stops it.
/// </summary>
/// <remarks>
/// The rule is that any unsent work refuses everything. The user deletes RomMBat's folder next,
/// and the outbox goes with it, so a partial removal that took the hooks and kept the outbox
/// would stop the very pass that sends it.
/// </remarks>
public sealed class UninstallTests : IDisposable
{
    private const string Ps2Game = "Armored Core 3 (USA).chd";
    private const string Ps2Key = "ps2[\"Armored Core 3 (USA).chd\"].pcsx2_slot1_memory";

    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly InstallSession _session;

    public UninstallTests()
    {
        _session = InstallSession.Open(_tree.Root).Session!;
    }

    public void Dispose()
    {
        _session.Dispose();
        _tree.Dispose();
    }

    [Fact]
    public async Task The_default_scope_takes_out_hooks_menu_and_conversions_and_leaves_the_library()
    {
        InstallHooksAndMenu();
        Rom(7, "snes", "Chrono Trigger (USA).sfc", FileOrigin.Synced);

        var report = Service().Preview(new RemovalScope());

        Assert.Equal(4, report.Hooks.Count);
        Assert.Equal(3, report.Menu.Count);
        Assert.Null(report.Content);

        var applied = await Service().ApplyAsync(report, TestContext.Current.CancellationToken);

        Assert.True(applied.Ok, applied.Refusal);
        Assert.False(new EsHooks(_session.Install).IsInstalled());
        Assert.False(new EsMenuEntry(_session.Install).IsInstalled());
        Assert.True(File.Exists(RomOnDisk("snes", "Chrono Trigger (USA).sfc")), "the library went with the default scope");
    }

    [Fact]
    public async Task A_conversion_is_put_back_even_after_its_rom_was_evicted()
    {
        // Eviction takes the ROM row and leaves the conversion, so the key names a file that is
        // gone. Reverting from the ROM refuses there; reverting from the record must not.
        Convert(PriorSettingState.Absent, prior: null);

        var applied = await Service().ApplyAsync(Service().Preview(new RemovalScope()), TestContext.Current.CancellationToken);

        Assert.True(applied.Ok, applied.Refusal);
        Assert.Equal(ConversionStatus.Reverted, Assert.Single(applied.Reverted!).Status);
        Assert.Null(Settings().Value(Ps2Key));
        Assert.Empty(_session.Store.SaveConversions.List());
    }

    [Fact]
    public async Task A_conversion_over_a_prior_value_puts_that_value_back()
    {
        Convert(PriorSettingState.Present, prior: "shared");

        await Service().ApplyAsync(Service().Preview(new RemovalScope()), TestContext.Current.CancellationToken);

        Assert.Equal("shared", Settings().Value(Ps2Key));
    }

    [Fact]
    public async Task A_conversion_somebody_changed_since_is_left_and_reported()
    {
        Convert(PriorSettingState.Absent, prior: null);
        WriteSetting(Ps2Key, "theirs");

        var applied = await Service().ApplyAsync(Service().Preview(new RemovalScope()), TestContext.Current.CancellationToken);

        Assert.False(applied.Ok);
        Assert.Equal(ConversionStatus.Refused, Assert.Single(applied.Reverted!).Status);
        Assert.Equal("theirs", Settings().Value(Ps2Key));
    }

    [Fact]
    public async Task A_queued_conversion_is_called_off()
    {
        _session.Store.PendingConfig.Queue(new PendingConfigRequest
        {
            RomId = 42,
            System = "ps2",
            FsName = Ps2Game,
            SettingKey = "pcsx2_slot1_memory",
            DesiredState = DesiredSettingState.Set,
            DesiredValue = "game",
            Reason = "a per-game memory card",
            QueuedAtUtc = Now,
        });

        var report = Service().Preview(new RemovalScope());
        Assert.Single(report.Queued);

        var applied = await Service().ApplyAsync(report, TestContext.Current.CancellationToken);

        Assert.Equal(1, applied.Cancelled);
        Assert.Empty(_session.Store.PendingConfig.ListOutstanding());
    }

    [Fact]
    public async Task An_unsent_save_refuses_the_whole_removal()
    {
        InstallHooksAndMenu();
        _session.Store.Outbox.Enqueue(OutboxKind.Save, Now, romId: 7, slot: "main");

        var report = Service().Preview(new RemovalScope());
        Assert.True(report.IsBlocked);

        var applied = await Service().ApplyAsync(report, TestContext.Current.CancellationToken);

        Assert.NotNull(applied.Refusal);
        Assert.Contains("flush", applied.Refusal, StringComparison.Ordinal);
        Assert.True(new EsHooks(_session.Install).IsInstalled(), "the hooks went while a save was still unsent");
    }

    [Fact]
    public async Task A_hook_event_still_in_the_spool_refuses_the_whole_removal()
    {
        // The spool is drained by a flush and lives in the folder the user deletes next.
        var spool = _session.Install.Resolve(Core.Sync.SpoolDrain.Directory);
        Directory.CreateDirectory(spool);
        File.WriteAllText(Path.Combine(spool, "0001" + Core.Sync.Spool.Extension), "game-end");

        var applied = await Service().ApplyAsync(Service().Preview(new RemovalScope()), TestContext.Current.CancellationToken);

        Assert.NotNull(applied.Refusal);
        Assert.Contains("spool", applied.Refusal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_refuses_while_EmulationStation_runs_and_changes_nothing()
    {
        InstallHooksAndMenu();
        Convert(PriorSettingState.Absent, prior: null);

        var running = new RemovalService(
            _session,
            () => EsRunningVerdict.Running("EmulationStation is running from this install (process 1234)."));

        var applied = await running.ApplyAsync(running.Preview(new RemovalScope()), TestContext.Current.CancellationToken);

        Assert.NotNull(applied.Refusal);
        Assert.True(new EsHooks(_session.Install).IsInstalled());
        Assert.Equal("game", Settings().Value(Ps2Key));
    }

    [Fact]
    public async Task Content_takes_synced_games_and_never_an_adopted_one()
    {
        Rom(7, "snes", "Chrono Trigger (USA).sfc", FileOrigin.Synced);
        Rom(8, "snes", "Mine (USA).sfc", FileOrigin.Adopted);

        var report = Service().Preview(new RemovalScope(Content: true));
        Assert.Equal([7], report.Content!.Plan.Selected.Select(candidate => candidate.File.RomId));

        var applied = await Service().ApplyAsync(report, TestContext.Current.CancellationToken);

        Assert.True(applied.Ok, applied.Refusal);
        Assert.False(File.Exists(RomOnDisk("snes", "Chrono Trigger (USA).sfc")));
        Assert.True(File.Exists(RomOnDisk("snes", "Mine (USA).sfc")));
    }

    [Fact]
    public async Task Bios_takes_synced_firmware_that_is_still_what_RomMBat_wrote()
    {
        var ours = Firmware("bios/scph5501.bin", [1, 2, 3, 4], FileOrigin.Synced);
        var changed = Firmware("bios/scph1001.bin", [5, 6, 7, 8], FileOrigin.Synced);
        var adopted = Firmware("bios/gba_bios.bin", [9, 9, 9, 9], FileOrigin.Adopted);

        File.WriteAllBytes(changed, [0, 0, 0, 0]);

        var applied = await Service().ApplyAsync(Service().Preview(new RemovalScope(Firmware: true)), TestContext.Current.CancellationToken);

        Assert.True(applied.Ok, applied.Refusal);
        Assert.Equal(1, applied.FirmwareRemoved);
        Assert.False(File.Exists(ours));
        Assert.True(File.Exists(changed), "firmware the user replaced was deleted");
        Assert.Single(applied.Kept!);
        Assert.True(File.Exists(adopted));
    }

    private RemovalService Service() => new(_session, () => EsRunningVerdict.NotRunning);

    private void InstallHooksAndMenu()
    {
        // Any bytes stand in for the hook: install copies, and uninstall deletes by name.
        var standIn = Path.Combine(_tree.Root, "stand-in-hook.exe");
        File.WriteAllBytes(standIn, [0x4D, 0x5A]);

        Assert.Equal(4, new EsHooks(_session.Install).Install(standIn).Installed);
        Assert.Equal(0, new EsMenuEntry(_session.Install).Install().Failed);
    }

    private void Convert(PriorSettingState state, string? prior)
    {
        WriteSetting(Ps2Key, "game");

        _session.Store.SaveConversions.Record(new SaveConversion
        {
            RomId = 42,
            System = "ps2",
            FsName = Ps2Game,
            SettingKey = "pcsx2_slot1_memory",
            AppliedValue = "game",
            PriorState = state,
            PriorValue = prior,
            ConvertedAtUtc = Now,
        });
    }

    private EsSettingsFile Settings() =>
        EsSettingsFile.Load(_session.Install.Resolve(EsSettingsFile.Location));

    private void WriteSetting(string key, string value)
    {
        var path = _session.Install.Resolve(EsSettingsFile.Location);
        var file = EsSettingsFile.Load(path);
        file.Set(key, value);
        file.WriteIfChanged(path);
    }

    private string RomOnDisk(string folder, string fileName) => Path.Combine(_tree.Root, "roms", folder, fileName);

    private void Rom(int romId, string folder, string fileName, FileOrigin origin)
    {
        var absolute = RomOnDisk(folder, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllBytes(absolute, new byte[64]);

        _session.Store.Files.Record(new LocalFile
        {
            Path = RelativePath.Create($"roms/{folder}/{fileName}"),
            Folder = folder,
            RomId = romId,
            Kind = LocalFileKind.Rom,
            FileName = fileName,
            SizeBytes = 64,
            Origin = origin,
        });
    }

    private string Firmware(string relative, byte[] bytes, FileOrigin origin)
    {
        var absolute = _session.Install.Resolve(RelativePath.Create(relative));
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllBytes(absolute, bytes);

        _session.Store.Files.Record(new LocalFile
        {
            Path = RelativePath.Create(relative),
            Kind = LocalFileKind.Firmware,
            FileName = Path.GetFileName(relative),
            SizeBytes = bytes.Length,
            Md5Hash = ContentHasher.ComputeFileMd5(absolute),
            Origin = origin,
        });

        return absolute;
    }
}
