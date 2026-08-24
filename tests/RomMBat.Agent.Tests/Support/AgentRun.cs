using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Agent.Tests.Support;

/// <summary>One invocation of a subcommand against a throwaway install, and what it printed.</summary>
/// <param name="ExitCode">What the process would have returned.</param>
/// <param name="Out">Everything written to stdout, which is where the reports go.</param>
/// <param name="Error">Everything written to stderr, which is where refusals go.</param>
public sealed record AgentRun(int ExitCode, string Out, string Error)
{
    public bool Wrote(string text) => Out.Contains(text, StringComparison.Ordinal);

    public bool Complained(string text) => Error.Contains(text, StringComparison.Ordinal);
}

/// <summary>
/// Runs a subcommand the way <c>Program</c> does, and captures what it said.
/// </summary>
/// <remarks>
/// <b>Through <see cref="Program.DispatchAsync"/>, not the command class.</b> The layer that
/// turns an exception into an exit code lives there, so a test that called
/// <c>BiosCommand.RunAsync</c> directly would run straight past it.
/// <para>
/// <b>Console is redirected rather than a writer being threaded through the commands.</b> The
/// reports go to <see cref="Console"/> directly today, and rebuilding that seam is a change to
/// shipped code made for the benefit of a test; redirecting is the smaller thing and it
/// exercises exactly the code a user runs. It is also why this collection is not parallel: two
/// tests swapping <c>Console.Out</c> at once would read each other's output.
/// </para>
/// <para>
/// Every run passes <c>--root</c>, so nothing here can find, open or write a real install.
/// </para>
/// </remarks>
internal static class AgentRunner
{
    public static async Task<AgentRun> RunAsync(TempRetroBatTree tree, params string[] args)
    {
        var command = CommandLine.Parse([.. args, "--root", tree.Root]);

        var output = new StringWriter();
        var error = new StringWriter();
        var previousOut = Console.Out;
        var previousError = Console.Error;

        try
        {
            Console.SetOut(output);
            Console.SetError(error);

            var exitCode = await Program.DispatchAsync(command, TestContext.Current.CancellationToken);

            return new AgentRun(exitCode, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousError);
        }
    }

    /// <summary>
    /// Puts RetroBat's shipped <c>es_systems.cfg</c> into a throwaway tree.
    /// </summary>
    /// <remarks>
    /// The live file is the authority on which systems an install has, and a tree without one
    /// declares nothing, so a command that validates its arguments against it would refuse
    /// everything. The upstream template is linked from <c>reference/</c> for the same reason
    /// the Core suite links it: it carries the parser traps a synthesized file does not.
    /// </remarks>
    public static void WriteEsSystems(TempRetroBatTree tree) =>
        CopyFixture(tree, "es_systems.template.cfg", "es_systems.cfg");

    /// <summary>
    /// Puts RetroBat's shipped <c>es_savestates.cfg</c> into a throwaway tree.
    /// </summary>
    /// <remarks>
    /// Without it <c>StateScanner.LoadSchema</c> answers null and no state is ever recorded, so
    /// a command whose behaviour turns on the state scan cannot be driven at all.
    /// </remarks>
    public static void WriteEsSaveStates(TempRetroBatTree tree) =>
        CopyFixture(tree, "es_savestates.template.cfg", "es_savestates.cfg");

    private static void CopyFixture(TempRetroBatTree tree, string fixture, string name)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "fixtures", fixture);
        var destination = Path.Combine(tree.Root, "emulationstation", ".emulationstation", name);

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }
}
