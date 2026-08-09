namespace RomMBat.Agent;

/// <summary>
/// A hand-rolled parser for the few options the agent takes.
/// </summary>
/// <remarks>
/// Deliberately not a package. The agent is a short-lived process launched from ES hooks,
/// and the whole option surface is a handful of flags; a command-line framework would be
/// more dependency than the problem is worth.
/// </remarks>
internal sealed class CommandLine
{
    private readonly Dictionary<string, string?> _options = new(StringComparer.OrdinalIgnoreCase);

    private CommandLine(string subcommand, IReadOnlyList<string> positional)
    {
        Subcommand = subcommand;
        Positional = positional;
    }

    public string Subcommand { get; }

    public IReadOnlyList<string> Positional { get; }

    /// <summary>Parses <c>--name value</c>, <c>--name=value</c> and bare <c>--flag</c> forms.</summary>
    public static CommandLine Parse(string[] args)
    {
        var subcommand = args.Length > 0 ? args[0] : string.Empty;
        var positional = new List<string>();
        var parsed = new CommandLine(subcommand, positional);

        for (var i = 1; i < args.Length; i++)
        {
            var argument = args[i];

            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                positional.Add(argument);
                continue;
            }

            var body = argument[2..];
            var equals = body.IndexOf('=', StringComparison.Ordinal);

            if (equals >= 0)
            {
                parsed._options[body[..equals]] = body[(equals + 1)..];
                continue;
            }

            var hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
            parsed._options[body] = hasValue ? args[++i] : null;
        }

        return parsed;
    }

    public bool Has(string name) => _options.ContainsKey(name);

    public string? Value(string name) => _options.TryGetValue(name, out var value) ? value : null;
}
