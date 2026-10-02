namespace RomMBat.Core.Sync;

/// <summary>
/// The one definition of which open <c>game-start</c> rows can still be a running game.
/// </summary>
/// <remarks>
/// A flush never closes a <c>game-start</c> that has no <c>game-end</c>, and a power loss
/// mid-game leaves exactly that. ES starting or quitting ends every game that was running, so a
/// row at or before the last <c>start</c> or <c>quit</c> is stale. Bounded by sequence rather
/// than a clock because the journal's order survives a flat RTC. Shared so
/// <see cref="InFlightGuard"/>, <see cref="Content.SaveGuard"/> and
/// <see cref="Sets.RemovalService"/> cannot drift apart.
/// </remarks>
internal static class RunningGames
{
    /// <summary>
    /// A predicate over <c>journal</c> rows: written after the last <c>start</c> or <c>quit</c>.
    /// </summary>
    public const string AfterLastFrontEndEvent =
        """
        local_sequence > (
              SELECT COALESCE(MAX(local_sequence), -1)
              FROM journal
              WHERE event IN ('start', 'quit'))
        """;
}
