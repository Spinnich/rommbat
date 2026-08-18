using Microsoft.Data.Sqlite;
using RomMBat.Core.Paths;

namespace RomMBat.Core.Store;

/// <summary>Which route taught us a binding.</summary>
public enum BindingSource
{
    /// <summary>A launch window covering the save's own mtime.</summary>
    Journal,

    /// <summary>A game code read out of the head of the ROM.</summary>
    RomHeader,

    /// <summary>The save-state name-mapping sidecar RetroBat writes.</summary>
    Sidecar,

    /// <summary>Somebody typed it, via <c>saves bind</c>.</summary>
    User,
}

/// <summary>
/// A learned Game ID to ROM binding, or a record that one could not be learned.
/// </summary>
/// <param name="RomId">
/// Null means <b>investigated and not bound</b>, which is a real answer rather than an absence:
/// either no route resolved the key, or two disagreed and the fail-closed rule refused to pick.
/// Without the row the same key is re-investigated and re-reported on every scan.
/// </param>
public sealed record GameIdBinding(
    string System,
    string GameId,
    long? RomId,
    RelativePath? RomPath,
    BindingSource LearnedFrom,
    string? Detail,
    DateTimeOffset LearnedAt)
{
    /// <summary>True when this names a ROM, rather than recording that nothing does.</summary>
    public bool IsResolved => RomId is not null;
}

/// <summary>
/// The cache that means an odd case is only worked out once.
/// </summary>
/// <remarks>
/// <b>A wrong binding is the worst outcome in this milestone</b>, because it uploads one game's
/// save under another game's name and the cache then makes the mistake permanent. So every path
/// that writes here fails closed: a key two routes disagree about is stored with a null
/// <c>rom_id</c> and reported, never guessed at, and <c>saves bind</c> is what a person uses to
/// settle or clear one. That command is why <c>learned_from</c> has a <c>'user'</c> value at all.
/// </remarks>
public sealed class GameIdBindingStore
{
    private readonly SqliteConnection _connection;

    internal GameIdBindingStore(SqliteConnection connection) => _connection = connection;

    /// <summary>Inserts or replaces the binding for a key.</summary>
    public void Record(GameIdBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        using var command = _connection.Command(
            """
            INSERT INTO game_id_binding (system, game_id, rom_id, rom_relative_path,
                                         learned_from, detail, learned_at)
            VALUES ($system, $gameId, $romId, $romPath, $source, $detail, $at)
            ON CONFLICT (system, game_id) DO UPDATE SET
              rom_id            = excluded.rom_id,
              rom_relative_path = excluded.rom_relative_path,
              learned_from      = excluded.learned_from,
              detail            = excluded.detail,
              learned_at        = excluded.learned_at;
            """)
            .With("$system", binding.System)
            .With("$gameId", binding.GameId)
            .With("$romId", SqliteValues.OrNull(binding.RomId))
            .With("$romPath", binding.RomPath.HasValue ? binding.RomPath.Value.Value : DBNull.Value)
            .With("$source", ToText(binding.LearnedFrom))
            .With("$detail", SqliteValues.OrNull(binding.Detail))
            .With("$at", SqliteValues.ToText(binding.LearnedAt));

        command.ExecuteNonQuery();
    }

    /// <summary>What is known about a key, including that nothing is.</summary>
    public GameIdBinding? Find(string system, string gameId)
    {
        using var command = _connection.Command(
            """
            SELECT system, game_id, rom_id, rom_relative_path, learned_from, detail, learned_at
            FROM game_id_binding
            WHERE system = $system AND game_id = $gameId;
            """)
            .With("$system", system)
            .With("$gameId", gameId);

        using var reader = command.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    /// <summary>Every binding, for <c>saves</c> to show what attribution rests on.</summary>
    public IReadOnlyList<GameIdBinding> List()
    {
        using var command = _connection.Command(
            """
            SELECT system, game_id, rom_id, rom_relative_path, learned_from, detail, learned_at
            FROM game_id_binding
            ORDER BY system, game_id;
            """);

        using var reader = command.ExecuteReader();
        var bindings = new List<GameIdBinding>();

        while (reader.Read())
        {
            bindings.Add(Map(reader));
        }

        return bindings;
    }

    /// <summary>
    /// Removes a binding so it can be learned again or replaced.
    /// </summary>
    /// <remarks>
    /// The answer to "a wrong binding is permanent because the cache makes it so". Forgetting
    /// one leaves the next scan free to re-run every route, which is what a user does after
    /// moving a ROM or correcting a library.
    /// </remarks>
    public bool Forget(string system, string gameId)
    {
        using var command = _connection
            .Command("DELETE FROM game_id_binding WHERE system = $system AND game_id = $gameId;")
            .With("$system", system)
            .With("$gameId", gameId);

        return command.ExecuteNonQuery() > 0;
    }

    private static GameIdBinding Map(SqliteDataReader reader)
    {
        var romPath = reader.GetStringOrNull(3);

        return new GameIdBinding(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt64OrNull(2),
            romPath is not null && RelativePath.TryCreate(romPath, out var parsed) ? parsed : null,
            ParseSource(reader.GetString(4)),
            reader.GetStringOrNull(5),
            reader.GetTimestampOrNull(6) ?? DateTimeOffset.UnixEpoch);
    }

    private static string ToText(BindingSource value) => value switch
    {
        BindingSource.Journal => "journal",
        BindingSource.RomHeader => "rom_header",
        BindingSource.Sidecar => "sidecar",
        BindingSource.User => "user",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static BindingSource ParseSource(string value) => value switch
    {
        "journal" => BindingSource.Journal,
        "rom_header" => BindingSource.RomHeader,
        "sidecar" => BindingSource.Sidecar,
        "user" => BindingSource.User,
        _ => throw new InvalidOperationException($"Unknown binding source '{value}' in the database."),
    };
}
