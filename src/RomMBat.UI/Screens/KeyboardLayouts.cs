namespace RomMBat.UI.Screens;

/// <summary>Which of EmulationStation's three keyboards is on screen.</summary>
public enum KeyboardLayout
{
    /// <summary>ES's <c>kbUs</c>, and what every language but two gets.</summary>
    UnitedStates,

    /// <summary>ES's <c>kbFr</c>, AZERTY.</summary>
    French,

    /// <summary>ES's <c>kbKr</c>, Hangul on the first two layers and QWERTY on the other two.</summary>
    Korean,
}

/// <summary>
/// EmulationStation's own keyboards, transcribed rather than approximated.
/// </summary>
/// <remarks>
/// <b>Source: <c>es-core/src/guis/GuiTextEditPopupKeyboard.cpp</c> in
/// <c>batocera-linux/batocera-emulationstation</c></b>, which RetroBat builds unmodified as
/// <c>RetroBat-Official/emulationstation</c>. The tables are compiled into
/// <c>emulationstation.exe</c> rather than shipped as data, so there is nothing for
/// <c>reference/refresh.sh</c> to pull and nothing on disk to read: this is a copy, and the
/// shape below is upstream's exactly so that re-checking it against a newer ES is a diff rather
/// than a reading exercise.
/// <para>
/// <b>Four rows of four.</b> Each key occupies one column across four consecutive rows, which
/// are its lower, upper, alted and alted-upper faces. <c>-colspan-</c> and <c>-rowspan-</c>
/// mark cells swallowed by the key to their left or above, and an empty string is a key that
/// exists, holds focus and does nothing, which is how the special layer's blank half behaves
/// upstream. Nothing here is skipped or compacted, so a cursor cannot be stranded and a
/// transcription slip shows up as a row of the wrong length.
/// </para>
/// <para>
/// <b>Only three exist.</b> Upstream has no other layout and no mechanism to load one, so a
/// German or Japanese install types on the US grid there and does here too.
/// </para>
/// </remarks>
public static class KeyboardLayouts
{
    /// <summary>Cells across, which every row of every layout fills exactly.</summary>
    public const int Columns = 13;

    /// <summary>The four faces of one key: lower, upper, alted, alted upper.</summary>
    public const int Faces = 4;

    private static readonly string[][] Us =
    [
        ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "_", "+", "DEL"],
        ["!", "@", "#", "$", "%", "^", "&", "*", "(", ")", "-", "=", "DEL"],
        ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "_", "+", "DEL"],
        ["!", "@", "#", "$", "%", "^", "&", "*", "(", ")", "-", "=", "DEL"],

        ["q", "w", "e", "r", "t", "y", "u", "i", "o", "p", "{", "}", "OK"],
        ["Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[", "]", "OK"],
        ["à", "ä", "è", "ë", "ì", "ï", "ò", "ö", "ù", "ü", "¨", "¿", "OK"],
        ["à", "ä", "è", "ë", "ì", "ï", "ò", "ö", "ù", "ü", "¨", "¿", "OK"],

        ["a", "s", "d", "f", "g", "h", "j", "k", "l", ";", "\"", "|", "-rowspan-"],
        ["A", "S", "D", "F", "G", "H", "J", "K", "L", ":", "'", "\\", "-rowspan-"],
        ["á", "â", "é", "ê", "í", "î", "ó", "ô", "ú", "û", "ñ", "¡", "-rowspan-"],
        ["á", "â", "é", "ê", "í", "î", "ó", "ô", "ú", "û", "ñ", "¡", "-rowspan-"],

        ["~", "z", "x", "c", "v", "b", "n", "m", ",", ".", "?", "ALT", "-colspan-"],
        ["`", "Z", "X", "C", "V", "B", "N", "M", "<", ">", "/", "ALT", "-colspan-"],
        ["€", "", "", "", "", "", "", "", "", "", "", "ALT", "-colspan-"],
        ["€", "", "", "", "", "", "", "", "", "", "", "ALT", "-colspan-"],

        Bottom,
        Bottom,
        Bottom,
        Bottom,
    ];

    private static readonly string[][] Fr =
    [
        ["&", "é", "\"", "'", "(", "#", "è", "!", "ç", "à", ")", "-", "DEL"],
        ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "@", "_", "DEL"],
        ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "@", "_", "DEL"],
        ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "@", "_", "DEL"],

        ["a", "z", "e", "r", "t", "y", "u", "i", "o", "p", "^", "$", "OK"],
        ["A", "Z", "E", "R", "T", "Y", "U", "I", "O", "P", "¨", "*", "OK"],
        ["à", "ä", "ë", "ì", "ï", "ò", "ö", "ü", "\\", "|", "§", "°", "OK"],
        ["à", "ä", "ë", "ì", "ï", "ò", "ö", "ü", "\\", "|", "§", "°", "OK"],

        ["q", "s", "d", "f", "g", "h", "j", "k", "l", "m", "ù", "`", "-rowspan-"],
        ["Q", "S", "D", "F", "G", "H", "J", "K", "L", "M", "%", "£", "-rowspan-"],
        ["á", "â", "ê", "í", "î", "ó", "ô", "ú", "û", "ñ", "¡", "¿", "-rowspan-"],
        ["á", "â", "ê", "í", "î", "ó", "ô", "ú", "û", "ñ", "¡", "¿", "-rowspan-"],

        ["<", "w", "x", "c", "v", "b", "n", ",", ";", ":", "=", "ALT", "-colspan-"],
        [">", "W", "X", "C", "V", "B", "N", "?", ".", "/", "+", "ALT", "-colspan-"],
        ["€", "[", "]", "{", "}", "", "", "", "", "", "", "ALT", "-colspan-"],
        ["€", "[", "]", "{", "}", "", "", "", "", "", "", "ALT", "-colspan-"],

        Bottom,
        Bottom,
        Bottom,
        Bottom,
    ];

    private static readonly string[][] Kr =
    [
        ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=", "DEL"],
        ["!", "@", "#", "$", "%", "^", "&", "*", "(", ")", "_", "+", "DEL"],
        ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=", "DEL"],
        ["!", "@", "#", "$", "%", "^", "&", "*", "(", ")", "_", "+", "DEL"],

        ["ㅂ", "ㅈ", "ㄷ", "ㄱ", "ㅅ", "ㅛ", "ㅕ", "ㅑ", "ㅐ", "ㅔ", "[", "]", "OK"],
        ["ㅃ", "ㅉ", "ㄸ", "ㄲ", "ㅆ", "ㅛ", "ㅕ", "ㅑ", "ㅒ", "ㅖ", "{", "}", "OK"],
        ["q", "w", "e", "r", "t", "y", "u", "i", "o", "p", "[", "]", "OK"],
        ["Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "{", "}", "OK"],

        ["ㅁ", "ㄴ", "ㅇ", "ㄹ", "ㅎ", "ㅗ", "ㅓ", "ㅏ", "ㅣ", ";", "'", "\\", "-rowspan-"],
        ["ㅁ", "ㄴ", "ㅇ", "ㄹ", "ㅎ", "ㅗ", "ㅓ", "ㅏ", "ㅣ", ":", "\"", "|", "-rowspan-"],
        ["a", "s", "d", "f", "g", "h", "j", "k", "l", ";", "'", "\\", "-rowspan-"],
        ["A", "S", "D", "F", "G", "H", "J", "K", "L", ":", "\"", "|", "-rowspan-"],

        ["ㅋ", "ㅌ", "ㅊ", "ㅍ", "ㅠ", "ㅜ", "ㅡ", ",", ".", "/", "`", "ALT", "-colspan-"],
        ["ㅋ", "ㅌ", "ㅊ", "ㅍ", "ㅠ", "ㅜ", "ㅡ", "<", ">", "?", "~", "ALT", "-colspan-"],
        ["z", "x", "c", "v", "b", "n", "m", ",", ".", "/", "`", "ALT", "-colspan-"],
        ["Z", "X", "C", "V", "B", "N", "M", "<", ">", "?", "~", "ALT", "-colspan-"],

        Bottom,
        Bottom,
        Bottom,
        Bottom,
    ];

    /// <summary>The bottom row, which is the same on every layer of every layout.</summary>
    private static string[] Bottom =>
    [
        "SHIFT", "-colspan-",
        "SPACE", "-colspan-", "-colspan-", "-colspan-", "-colspan-", "-colspan-", "-colspan-",
        "RESET", "-colspan-",
        "CANCEL", "-colspan-",
    ];

    /// <summary>The raw table for one layout, in upstream's own shape.</summary>
    public static string[][] Table(KeyboardLayout layout) => layout switch
    {
        KeyboardLayout.French => Fr,
        KeyboardLayout.Korean => Kr,
        _ => Us,
    };

    /// <summary>
    /// Picks the layout for the language EmulationStation is running in.
    /// </summary>
    /// <param name="language">
    /// <c>es_settings.cfg</c>'s <c>Language</c>, which is where ES reads its own from on a
    /// Windows release build. Null or empty is the ordinary case, because ES prunes the setting
    /// when it matches its default.
    /// </param>
    /// <remarks>
    /// <b>Upstream lowercases only when the value carries a region.</b> It splits on <c>_</c>
    /// and lowercases the part before it, so <c>fr_FR</c> resolves and a bare <c>FR</c> would
    /// not. That is a slip rather than a rule, and copying it would mean matching a bug for the
    /// sake of matching, so the whole value is lowercased here.
    /// </remarks>
    public static KeyboardLayout For(string? language)
    {
        var name = language?.Trim().ToLowerInvariant() ?? string.Empty;

        var region = name.IndexOf('_', StringComparison.Ordinal);
        if (region >= 0)
        {
            name = name[..region];
        }

        return name switch
        {
            "fr" => KeyboardLayout.French,
            "ko" => KeyboardLayout.Korean,
            _ => KeyboardLayout.UnitedStates,
        };
    }
}
