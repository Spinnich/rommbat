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
    /// <summary>One connection's gate, plus whether that connection is being closed.</summary>
    private sealed class Gate
    {
        /// <summary>
        /// Set while the owning thread is closing the connection, which makes
        /// <see cref="Leave"/> inert.
        /// </summary>
        /// <remarks>
        /// Closing disposes the commands the connection still tracks, on the closing thread, and
        /// each fires the <c>Disposed</c> handler <c>SqliteValues.Command</c> attached. That
        /// handler would find the gate entered, because the closing thread is the one holding it,
        /// and release it half way through the close, letting another thread back onto a
        /// connection that is being torn down. Only the closing thread can be inside while this
        /// is set, since it is set after the gate is taken.
        /// <para>
        /// <b>Nothing reaches that today, and it is the ordering rather than this flag that
        /// stops it.</b> A command still tracked when the close begins is one its creating
        /// thread has not disposed, so that thread holds the gate and
        /// <see cref="EnterForClose"/> has not returned; a command disposed beforehand is no
        /// longer tracked, so <c>Close</c> never re-fires its <c>Disposed</c>. Counting entries
        /// to <see cref="Leave"/> taken while this is set found none across the whole suite, and
        /// removing the flag and the guard in <see cref="Leave"/> fails nothing. Keep it as the
        /// assumption written down, not as something a test covers: it starts earning its place
        /// the moment a second path closes the connection, or disposes a command, without
        /// holding the gate.
        /// </para>
        /// </remarks>
        internal bool Closing;
    }

    private static readonly ConditionalWeakTable<SqliteConnection, Gate> Gates = [];

    internal static void Enter(SqliteConnection connection) => Monitor.Enter(GateFor(connection));

    /// <summary>Takes the gate for a close, and stops the close releasing it from underneath.</summary>
    /// <remarks>
    /// Disposal has to be ordered against readers like everything else. It used to close the
    /// connection with no gate at all, so <c>SqliteConnection.Close</c> enumerated its
    /// prepared-statement list while a background reader was still mutating it. That throws out
    /// of <c>Dispose</c> as either "Collection was modified" or an
    /// <c>ObjectDisposedException</c> naming <c>SQLitePCL.sqlite3_stmt</c>, depending on which
    /// of the two the close reaches first, so neither string alone identifies it.
    /// </remarks>
    internal static void EnterForClose(SqliteConnection connection)
    {
        var gate = GateFor(connection);

        Monitor.Enter(gate);
        gate.Closing = true;
    }

    /// <summary>Releases the gate a close took, whether or not the close succeeded.</summary>
    /// <remarks>
    /// The gate is released rather than held for ever, so a thread that arrives afterwards is
    /// answered by the disposed connection with an ordinary exception instead of blocking on a
    /// gate nothing will ever open.
    /// </remarks>
    internal static void LeaveAfterClose(SqliteConnection connection)
    {
        var gate = GateFor(connection);

        gate.Closing = false;

        if (Monitor.IsEntered(gate))
        {
            Monitor.Exit(gate);
        }
    }

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

        // A close owns the gate for its whole duration and releases it itself.
        if (gate.Closing || !Monitor.IsEntered(gate))
        {
            return;
        }

        Monitor.Exit(gate);
    }

    private static Gate GateFor(SqliteConnection connection) =>
        Gates.GetValue(connection, _ => new Gate());
}
