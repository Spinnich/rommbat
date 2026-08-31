using System.Text;
using RomMBat.Core.RetroBat;

namespace RomMBat.Core.Mapping;

/// <summary>Which layer of the chain answered. Persisted as <c>platform_map.resolved_by</c>.</summary>
/// <remarks>
/// The values divide into three kinds, and the mapping screen shows the difference:
/// <see cref="User"/> is a choice, <see cref="FsSlug"/> and <see cref="Bundled"/> are applied
/// guesses, and <see cref="Normalized"/> is a suggestion that is deliberately <b>not</b>
/// applied. Accepting a suggestion rewrites the row as <see cref="User"/>.
/// </remarks>
public enum MappingSource
{
    /// <summary>Set by the user. Always wins, and roams in <c>Device.sync_config</c>.</summary>
    User,

    /// <summary><c>platform.fs_slug</c> is already a folder in the live <c>es_systems.cfg</c>.</summary>
    FsSlug,

    /// <summary>The bundled table named a folder the install has.</summary>
    Bundled,

    /// <summary>A normalized name matched. Offered for confirmation, never applied.</summary>
    Normalized,

    /// <summary>Nothing matched. A normal state, not an error.</summary>
    Unmapped,
}

/// <summary>The RomM side of one platform, as much of it as mapping needs.</summary>
/// <param name="Slug">
/// RomM's platform slug, which is what the bundled table is looked up by. <b>Not unique.</b>
/// A real 123-platform library carried 72 distinct slugs, because every system has an
/// "-unofficial" twin sharing one.
/// </param>
/// <param name="FsSlug">
/// RomM's folder name for the platform, which RomM does keep unique. It is both layer 2 of
/// the chain and the identity everything downstream keys on.
/// </param>
public sealed record RomMPlatform(int Id, string Slug, string? FsSlug, string? DisplayName, int RomCount = 0)
{
    /// <summary>The identifier the map is keyed by, falling back to the slug if RomM sent none.</summary>
    public string Key => string.IsNullOrWhiteSpace(FsSlug) ? Slug : FsSlug;
}

/// <summary>What the chain decided for one platform.</summary>
/// <param name="FsSlug">The platform's identity, and the key of its row in the map.</param>
/// <param name="Folder">
/// The RetroBat folder to write to, or null when nothing was applied. Null is not a failure:
/// a suggestion awaiting confirmation and a platform with no RetroBat equivalent both land
/// here, and <see cref="ResolvedBy"/> tells them apart.
/// </param>
/// <param name="Suggestion">A folder offered for confirmation. Set only for <see cref="MappingSource.Normalized"/>.</param>
/// <param name="Candidates">Every folder the bundled table names for this slug, best first, for the mapping screen.</param>
public sealed record PlatformResolution(
    string FsSlug,
    string Slug,
    int? PlatformId,
    string? DisplayName,
    string? Folder,
    MappingSource ResolvedBy,
    string? Suggestion,
    IReadOnlyList<string> Candidates,
    bool RequiresExplicitChoice,
    bool FolderMissingFromInstall,
    string Explanation)
{
    /// <summary>True when there is a folder to sync into.</summary>
    public bool IsApplied => Folder is not null;
}

/// <summary>
/// The five-layer chain that turns a RomM platform into a RetroBat folder.
/// </summary>
/// <remarks>
/// The two vocabularies genuinely diverge, so this is a feature with a visible, editable
/// surface rather than a lookup. 91 of RetroBat's 240 systems have no seed mapping at all,
/// and about 50 of the hard cases are ports and storefronts (<c>cavestory</c>,
/// <c>devilutionx</c>, <c>steam</c>, <c>gog</c>) which have no RomM platform by design.
/// <para>
/// <b>Only one kind of unmapped is the user's problem.</b> A RomM platform with no RetroBat
/// folder is reported and skipped. A RetroBat folder with no RomM platform is ignored
/// entirely, which is why nothing here ever enumerates folders that no platform claimed.
/// </para>
/// </remarks>
public sealed class PlatformResolver
{
    private readonly EsSystemsFile _install;
    private readonly BundledPlatformMap _bundled;
    private readonly IReadOnlyDictionary<string, string> _overrides;
    private readonly Lazy<IReadOnlyDictionary<string, string>> _normalizedFolders;

    /// <param name="install">The live <c>es_systems.cfg</c>, which is the authority on what folders exist.</param>
    /// <param name="userOverrides">RomM <c>fs_slug</c> to folder, from the mapping table. Case-insensitive.</param>
    /// <param name="bundled">The shipped table. Defaults to the embedded one.</param>
    public PlatformResolver(
        EsSystemsFile install,
        IReadOnlyDictionary<string, string>? userOverrides = null,
        BundledPlatformMap? bundled = null)
    {
        ArgumentNullException.ThrowIfNull(install);

        _install = install;
        _bundled = bundled ?? BundledPlatformMap.Bundled;
        _overrides = userOverrides ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _normalizedFolders = new Lazy<IReadOnlyDictionary<string, string>>(BuildNormalizedFolders);
    }

    /// <summary>
    /// Strips case and punctuation, which is what turns <c>actionmax</c> into
    /// <c>action-max</c> and <c>ti99</c> into <c>ti-99</c>.
    /// </summary>
    /// <remarks>
    /// It resolves 16 of the 91 unmapped systems and no more, so it is offered rather than
    /// applied: the remaining 75 would produce confident nonsense.
    /// </remarks>
    public static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    /// <summary>Runs the chain for one platform.</summary>
    public PlatformResolution Resolve(RomMPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(platform);

        var candidates = _bundled.Candidates(platform.Slug);
        var mustChoose = _bundled.RequiresExplicitChoice(platform.Slug, out var whyChoose);

        // 1. User override. Always wins, including over a slug that would otherwise demand a
        //    choice, because making the choice is exactly what setting an override is.
        if (_overrides.TryGetValue(platform.Key, out var chosen) && !string.IsNullOrWhiteSpace(chosen))
        {
            var missing = !_install.HasFolder(chosen);
            return new PlatformResolution(
                platform.Key,
                platform.Slug,
                platform.Id,
                platform.DisplayName,
                chosen,
                MappingSource.User,
                null,
                candidates,
                mustChoose,
                missing,
                missing
                    ? $"Set by you to '{chosen}', which this install does not have. Games would sync to a "
                        + "folder EmulationStation never scans."
                    : $"Set by you to '{chosen}'.");
        }

        // 2. fs_slug against the live file. When someone's RomM library is already laid out
        //    Batocera-style, fs_slug is the folder name and no translation is needed.
        //
        //    This runs ahead of the arcade check below, and the order is the whole point. An
        //    arcade-shaped slug is ambiguous because ten folders could be right; an fs_slug
        //    that already names one of them is not ambiguous at all, because the person filing
        //    the library has answered the question by naming the folder. Refusing anyway made
        //    a platform whose fs_slug is literally 'fbneo', on an install that has an 'fbneo'
        //    system and a roms/fbneo directory, demand a per-set choice it did not need, and
        //    it did so halfway through resolving a collection that merely happened to contain
        //    an arcade game.
        if (_install.TryGetFolder(platform.FsSlug, out var byFsSlug))
        {
            return Applied(
                platform,
                byFsSlug.Folder,
                MappingSource.FsSlug,
                candidates,
                $"RomM's fs_slug '{platform.FsSlug}' is already a folder in this install.");
        }

        // An arcade-shaped slug stops here, once the fs_slug has failed to answer it. Which
        // folder is right depends on the romset the file came from, and arcade rom names are
        // romset-versioned, so every remaining layer would be guessing. Candidates are still
        // reported so the choice can be offered.
        if (mustChoose)
        {
            return new PlatformResolution(
                platform.Key,
                platform.Slug,
                platform.Id,
                platform.DisplayName,
                null,
                MappingSource.Unmapped,
                null,
                candidates,
                RequiresExplicitChoice: true,
                FolderMissingFromInstall: false,
                whyChoose);
        }

        // 3. Bundled table, first candidate the install actually has.
        foreach (var candidate in candidates)
        {
            if (_install.TryGetFolder(candidate, out var bundled))
            {
                return Applied(
                    platform,
                    bundled.Folder,
                    MappingSource.Bundled,
                    candidates,
                    candidates.Count > 1
                        ? $"From the bundled table, which offers {string.Join(", ", candidates)} and picked the "
                            + "first one this install has."
                        : "From the bundled table.");
            }
        }

        // 4. Normalized match, offered rather than applied.
        if (TryNormalizedMatch(platform, out var suggestion))
        {
            return new PlatformResolution(
                platform.Key,
                platform.Slug,
                platform.Id,
                platform.DisplayName,
                null,
                MappingSource.Normalized,
                suggestion,
                candidates,
                RequiresExplicitChoice: false,
                FolderMissingFromInstall: false,
                $"'{platform.Slug}' looks like the folder '{suggestion}'. Confirm it before anything syncs there.");
        }

        // 5. Unmapped. Normal, and the only kind of unmapped worth showing anyone.
        return new PlatformResolution(
            platform.Key,
            platform.Slug,
            platform.Id,
            platform.DisplayName,
            null,
            MappingSource.Unmapped,
            null,
            candidates,
            RequiresExplicitChoice: false,
            FolderMissingFromInstall: candidates.Count > 0,
            candidates.Count > 0
                ? $"'{platform.Slug}' maps to {string.Join(", ", candidates)}, none of which this install has. "
                    + "Add the system in RetroBat, or pick a folder yourself."
                : $"This install has no RetroBat system for '{platform.Slug}'. Its games are skipped.");
    }

    /// <summary>Runs the chain over a whole platform list, in the order given.</summary>
    public IReadOnlyList<PlatformResolution> ResolveAll(IEnumerable<RomMPlatform> platforms)
    {
        ArgumentNullException.ThrowIfNull(platforms);
        return [.. platforms.Select(Resolve)];
    }

    private static PlatformResolution Applied(
        RomMPlatform platform,
        string folder,
        MappingSource source,
        IReadOnlyList<string> candidates,
        string explanation) =>
        new(
            platform.Key,
            platform.Slug,
            platform.Id,
            platform.DisplayName,
            folder,
            source,
            null,
            candidates,
            false,
            false,
            explanation);

    private bool TryNormalizedMatch(RomMPlatform platform, out string folder)
    {
        var folders = _normalizedFolders.Value;

        foreach (var key in new[] { platform.Slug, platform.FsSlug })
        {
            if (!string.IsNullOrWhiteSpace(key) && folders.TryGetValue(Normalize(key), out folder!))
            {
                return true;
            }
        }

        folder = string.Empty;
        return false;
    }

    private Dictionary<string, string> BuildNormalizedFolders()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var system in _install.Systems)
        {
            // First wins, so a later duplicate cannot quietly redirect an earlier system.
            map.TryAdd(Normalize(system.Folder), system.Folder);
        }

        return map;
    }
}
