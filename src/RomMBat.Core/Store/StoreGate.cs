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
/// <b>Re-entrant, deliberately.</b> <see cref="LocalStore.InTransaction"/> holds the gate for the
/// whole transaction and the store calls inside it take it again; <c>Monitor</c> lets the owning
/// thread back in, which is the only reason a transaction does not deadlock against itself.
/// </para>
/// <para>
/// <b>A command must be created and disposed on the same thread, and that is a real
/// constraint rather than an incidental one.</b> <c>Monitor</c> belongs to the thread that took
/// it, so an <c>await</c> between opening a command and disposing it can resume elsewhere and
/// the release then throws instead of letting go, leaving the gate held for ever. Every store
/// method is synchronous today, which is what makes this safe; an <c>async</c> one would need a
/// different primitive, and a re-entrant one, because <see cref="LocalStore.InTransaction"/>
/// depends on re-entry.
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
