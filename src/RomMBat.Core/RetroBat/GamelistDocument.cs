using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace RomMBat.Core.RetroBat;

/// <summary>
/// One entry to merge into a gamelist: which game, and the elements the caller owns.
/// </summary>
/// <remarks>
/// Deliberately a bag of strings rather than a typed game. This is the seam M7 reuses for
/// <c>system/es_menu/gamelist.xml</c>, whose entry is an application rather than a ROM, so
/// nothing here knows what a ROM is.
/// </remarks>
/// <param name="Path">The <c>&lt;path&gt;</c> text, which is the key. Always <c>./name</c>.</param>
/// <param name="Fields">
/// Element name to value, in the order a new entry writes them. A null value removes that
/// element, which is how a field that stopped having a value stops being asserted.
/// </param>
public sealed record GamelistEntry(string Path, IReadOnlyList<KeyValuePair<string, string?>> Fields);

/// <summary>What one merge changed.</summary>
public sealed record GamelistMergeResult
{
    public int Added { get; init; }

    public int Updated { get; init; }

    public int Removed { get; init; }

    /// <summary>Entries left exactly as they were, which is every entry on a second run.</summary>
    public int Unchanged { get; init; }

    /// <summary>Entries in the file that RomMBat does not own and did not touch.</summary>
    public int Foreign { get; init; }

    /// <summary>True when the rendered document is byte-identical to the file on disk.</summary>
    public bool IsUnchanged { get; init; }

    public bool Wrote => !IsUnchanged;
}

/// <summary>
/// Reads, merges and writes a <c>gamelist.xml</c> without knowing what a game is.
/// </summary>
/// <remarks>
/// <b>Another process owns this file.</b> EmulationStation's own scraper writes entries into
/// it, and ES writes play statistics back into the same entries RomMBat wrote. So the merge
/// is an <b>allowlist of the elements the caller names</b>, never a blocklist of the ones ES
/// is known to write: a real install carries <c>playcount</c>, <c>lastplayed</c>,
/// <c>gametime</c>, <c>scrap</c> with its attributes, <c>id</c> and <c>source</c> on
/// <c>&lt;game&gt;</c>, <c>cheevosHash</c>, <c>cheevosId</c>, <c>md5</c>, <c>crc32</c>,
/// <c>arcadesystemname</c> and <c>multidisk</c>, and the four fields the plan first named do
/// not cover half of them.
/// <para>
/// <b>Nothing here relies on ES preserving what it read.</b> Measured: when ES has a reason
/// to rewrite the file it drops every XML comment, moves the entry it changed to the end and
/// rewrites that entry's children into its own order. Comments in a file RomMBat wrote are
/// therefore preserved by this class and may still be lost by ES, so they are never used to
/// carry meaning.
/// </para>
/// <para>
/// Output is deterministic: same input, same bytes. That is what makes the no-churn
/// regression a real assertion rather than a hope.
/// </para>
/// </remarks>
public sealed class GamelistDocument
{
    /// <summary>The declaration ES itself writes. No encoding attribute, and no BOM.</summary>
    private const string Declaration = "<?xml version=\"1.0\"?>";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly XDocument _document;
    private readonly XElement _root;

    private GamelistDocument(XDocument document)
    {
        _document = document;
        _root = document.Root!;
    }

    /// <summary>An empty gamelist, for a folder that has never had one.</summary>
    public static GamelistDocument Empty() => new(new XDocument(new XElement("gameList")));

    /// <summary>
    /// Reads a gamelist from disk, or returns an empty one when the file is not there.
    /// </summary>
    /// <exception cref="GamelistParseException">
    /// The file exists and is not a gamelist. Thrown rather than swallowed, because the
    /// alternative is overwriting a file whose contents could not be read, and the user's
    /// scraped metadata is in it.
    /// </exception>
    public static GamelistDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return Empty();
        }

        try
        {
            // DTD processing off and no resolver: this file comes from the user's disk, but a
            // gamelist has no legitimate use for an external entity and the parse runs with
            // whatever rights the agent has.
            //
            // IgnoreWhitespace is what makes the output deterministic. Left off, the reader
            // hands XDocument the indentation as text nodes, the writer then treats every
            // element as having mixed content and stops indenting, and re-rendering a file
            // this class wrote produces different bytes from the ones it wrote. That would
            // make the no-churn rule unsatisfiable rather than merely untested.
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreWhitespace = true,
            };
            using var stream = File.OpenRead(path);
            using var reader = XmlReader.Create(stream, settings);

            var document = XDocument.Load(reader, LoadOptions.None);

            if (document.Root is null || !document.Root.Name.LocalName.Equals("gameList", StringComparison.Ordinal))
            {
                throw new GamelistParseException(path, "its root element is not <gameList>.");
            }

            return new GamelistDocument(document);
        }
        catch (XmlException ex)
        {
            throw new GamelistParseException(path, ex.Message, ex);
        }
    }

    /// <summary>How many <c>&lt;game&gt;</c> entries the document holds.</summary>
    public int Count => _root.Elements("game").Count();

    /// <summary>The <c>&lt;path&gt;</c> of every entry, in document order.</summary>
    public IReadOnlyList<string> Paths =>
        [.. _root.Elements("game").Select(PathOf).Where(path => path is not null).Select(path => path!)];

    /// <summary>
    /// Applies one entry, creating it when it is not there.
    /// </summary>
    /// <returns>True when the document changed.</returns>
    public bool Apply(GamelistEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var game = Find(entry.Path);
        var added = game is null;

        if (game is null)
        {
            game = new XElement("game", new XElement("path", entry.Path));
            _root.Add(game);
        }

        var changed = added;

        foreach (var (name, value) in entry.Fields)
        {
            if (name.Equals("path", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var existing = game.Element(name);
            var cleaned = value is null ? null : XmlText.Sanitize(value);

            if (cleaned is null)
            {
                // Removing an element RomMBat owns is how a value that stopped existing stops
                // being asserted. It never reaches an element it does not own.
                if (existing is not null)
                {
                    existing.Remove();
                    changed = true;
                }

                continue;
            }

            if (existing is null)
            {
                // Appended rather than inserted in a canonical position: an entry a user's own
                // scraper wrote keeps its shape, and appending is stable across runs, which is
                // what the no-churn assertion needs.
                game.Add(new XElement(name, cleaned));
                changed = true;
                continue;
            }

            if (!string.Equals(existing.Value, cleaned, StringComparison.Ordinal))
            {
                existing.Value = cleaned;
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>Removes an entry by path. Returns false when it was not there.</summary>
    public bool Remove(string path)
    {
        var game = Find(path);
        if (game is null)
        {
            return false;
        }

        game.Remove();
        return true;
    }

    /// <summary>True when an entry with this path exists.</summary>
    public bool Contains(string path) => Find(path) is not null;

    /// <summary>
    /// Renders the document exactly as it would be written.
    /// </summary>
    /// <remarks>
    /// Tab-indented with LF endings and no BOM, matching what EmulationStation writes, so a
    /// file that passes through both processes changes as little as possible.
    /// </remarks>
    public string Render()
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "\t",
            NewLineChars = "\n",
            OmitXmlDeclaration = true,
            Encoding = Utf8NoBom,
        };

        var builder = new StringBuilder();
        builder.Append(Declaration).Append('\n');

        using (var writer = XmlWriter.Create(builder, settings))
        {
            _document.Save(writer);
        }

        builder.Append('\n');
        return builder.ToString();
    }

    /// <summary>
    /// Writes the document, and does not write when the bytes would be the same.
    /// </summary>
    /// <remarks>
    /// The comparison is against the file on disk rather than against anything remembered,
    /// which is the only version that is true after ES has had the file. Written to a temp
    /// file in the same directory and renamed, so a power cut leaves either the old file or
    /// the new one and never half of either.
    /// </remarks>
    /// <returns>True when bytes were written.</returns>
    public bool WriteIfChanged(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var rendered = Utf8NoBom.GetBytes(Render());

        if (File.Exists(path))
        {
            var existing = File.ReadAllBytes(path);
            if (existing.AsSpan().SequenceEqual(rendered))
            {
                return false;
            }
        }

        var directory = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = path + ".rommbat-tmp";
        File.WriteAllBytes(temporary, rendered);
        File.Move(temporary, path, overwrite: true);
        return true;
    }

    private XElement? Find(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var wanted = NormalizePath(path);
        return _root.Elements("game")
            .FirstOrDefault(game => string.Equals(NormalizePath(PathOf(game)), wanted, StringComparison.OrdinalIgnoreCase));
    }

    private static string? PathOf(XElement game) => game.Element("path")?.Value;

    /// <summary>
    /// Compares paths the way the filesystem does, and the way a hand-edited file might differ.
    /// </summary>
    /// <remarks>
    /// A gamelist written by hand or by another tool can carry <c>Game.zip</c> where RomMBat
    /// writes <c>./Game.zip</c>, and Windows paths are case-insensitive. Treating those as
    /// different entries would duplicate every game in the file.
    /// </remarks>
    private static string? NormalizePath(string? path)
    {
        if (path is null)
        {
            return null;
        }

        var trimmed = path.Trim().Replace('\\', '/');
        return trimmed.StartsWith("./", StringComparison.Ordinal) ? trimmed[2..] : trimmed;
    }
}

/// <summary>A gamelist that exists and could not be read.</summary>
/// <remarks>
/// Its own type because the only safe response is to leave the file alone: it holds metadata
/// a user may have scraped or edited by hand, and rewriting what could not be parsed would
/// destroy it.
/// </remarks>
public sealed class GamelistParseException : Exception
{
    public GamelistParseException(string path, string reason, Exception? inner = null)
        : base($"'{path}' could not be read as a gamelist: {reason} It has been left alone.", inner) =>
        Path = path;

    public GamelistParseException()
    {
    }

    public GamelistParseException(string message)
        : base(message)
    {
    }

    public GamelistParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public string? Path { get; }
}
