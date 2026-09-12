using RomM.Client;
using RomMBat.Core;

namespace RomMBat.UI.Screens;

/// <summary>
/// Getting a connection the way every screen that talks to RomM needs one.
/// </summary>
/// <remarks>
/// <b>One body, because three screens had grown their own and one of them was wrong.</b> Browse
/// kept a cached connection written on a loader thread and read on the thread that draws
/// (#118); the removal flush built one inline and disposed the wrong half on one branch. The
/// dance is the same in all of them: authenticate through <see cref="InstallSession"/>, and when
/// a test has handed in a factory, drop what authenticating produced and use the factory's
/// instead so a stub can stand in its place.
/// <para>
/// <b>Null is offline, not an error.</b> Not paired, no token, no server: all of them are the
/// ordinary state on a handheld away from its server, and a caller branches on the null rather
/// than catching something.
/// </para>
/// </remarks>
public static class UiConnection
{
    /// <summary>
    /// Opens a connection, or answers null when there is nothing to connect with.
    /// </summary>
    /// <param name="connect">
    /// A factory a test stands in place of the real connection, the way every screen that talks
    /// to RomM already accepts one. Null uses whatever authenticating produced.
    /// </param>
    /// <remarks>
    /// The caller owns disposing what comes back.
    /// </remarks>
    public static RomMConnection? Open(InstallSession session, Func<Uri, RomMConnection>? connect)
    {
        ArgumentNullException.ThrowIfNull(session);

        var attempt = session.Authenticate();

        if (attempt.Connection is null)
        {
            return null;
        }

        var origin = session.Store.Device.Read()?.ServerOrigin;

        if (connect is null || origin is null)
        {
            return attempt.Connection;
        }

        // Disposed rather than leaked. The factory's connection replaces it, and the one
        // authenticating built holds a handler with sockets of its own.
        attempt.Connection.Dispose();
        return connect(origin);
    }
}
