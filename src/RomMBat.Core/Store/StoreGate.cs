using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;

namespace RomMBat.Core.Store;

/// <summary>
/// Orders the threads inside this process that share one <see cref="SqliteConnection"/>.
/// </summary>
/// <remarks>
/// <b>Keyed on the connection rather than held by the store</b>, because every store class takes
/// the raw connection and there is no single object all of them can see. A conditional weak
/// table means a connection that goes away takes its gate with it, so nothing accumulates across
/// the many short-lived stores the tests open.
/// <para>
/// <b>Re-entrant, deliberately.</b> <see cref="LocalStore.InTransaction"/> takes the gate itself
/// for the whole transaction and the store calls inside it take it again; <c>Monitor</c> lets the
/// owning thread back in, which is the only reason a transaction does not deadlock against
/// itself. Leaving the transaction to its inner calls would not do: <c>BEGIN</c> and
/// <c>COMMIT</c> go through the connection directly, so the gate would be dropped between
/// statements.
/// </para>
/// <para>
/// <b>A command must be created and disposed on the same thread, and that is a real
/// constraint rather than an incidental one.</b> <c>Monitor</c> belongs to the thread that took
/// it, so an <c>await</c> between opening a command and disposing it can resume elsewhere and
/// the release then finds a gate this thread does not hold, leaving it held for ever with no
/// stack trace to say why. Every store method is synchronous today, which is what makes this
/// safe; an <c>async</c> one would need a different primitive, and a re-entrant one, because
/// <see cref="LocalStore.InTransaction"/> depends on re-entry. See <see cref="Leave"/> for why
/// that release returns rather than throwing, and for the one place it legitimately happens.
/// </para>
/// <para>
/// <b>This says nothing about other processes.</b> The database is WAL and the hooks write to it
/// from their own processes; the tree lock and the busy timeout are what order those.
/// </para>
/// </remarks>
internal static class StoreGate
{
    private static readonly ConditionalWeakTable<SqliteConnection, object> Gates = [];

    internal static void Enter(SqliteConnection connection) => Monitor.Enter(GateFor(connection));

    /// <summary>
    /// Releases the gate, unless this thread is not the one holding it.
    /// </summary>
    /// <remarks>
    /// <b>The guard is load-bearing and the case is disposal, not an <c>await</c>.</b>
    /// <c>SqliteConnection.Close</c> disposes every command the connection is still tracking, on
    /// whatever thread closed it, and each of those fires the <c>Disposed</c> handler
    /// <c>SqliteValues.Command</c> attached. A background loader that is still mid-read when the
    /// store is disposed therefore has its command released by the disposing thread, and an
    /// unguarded <c>Monitor.Exit</c> throws <c>SynchronizationLockException</c> out of
    /// <c>LocalStore.Dispose</c>. Removing the guard was tried and two tests caught it.
    /// <para>
    /// What it costs is that the abandoned command's gate is never released. That is only ever a
    /// gate on a connection that is being closed, because every call site disposes inside a
    /// <c>using</c>, so nothing can wait on it afterwards. A release from the wrong thread on a
    /// <b>live</b> connection would leave a hold nothing can clear, which is the failure the
    /// type's remarks describe: it is prevented by every store method being synchronous, not by
    /// this.
    /// </para>
    /// </remarks>
    internal static void Leave(SqliteConnection connection)
    {
        var gate = GateFor(connection);

        if (Monitor.IsEntered(gate))
        {
            Monitor.Exit(gate);
        }
    }

    private static object GateFor(SqliteConnection connection) =>
        Gates.GetValue(connection, _ => new object());
}
