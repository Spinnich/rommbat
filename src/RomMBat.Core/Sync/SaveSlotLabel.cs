namespace RomMBat.Core.Sync;

/// <summary>
/// What a player reads in place of a slot, such as "Battery save" for <c>libretro:battery</c>.
/// </summary>
/// <remarks>
/// <b>The gamepad UI and every flush problem show these, and the agent's conflict listing keeps
/// the raw slot</b>, because the slot is what <c>saves resolve</c> takes and a label cannot be typed
/// back. The emulator half is dropped:
/// the game's own row already says which game it is, and two rows differ by the kind of save.
/// <para>
/// A qualifier after <c>battery</c> stays in brackets, because a game can hold two battery saves
/// that differ by nothing else, and two rows reading "Battery save" could not be told apart.
/// </para>
/// </remarks>
public static class SaveSlotLabel
{
    /// <summary>The plain label for a slot, or "Save" for none.</summary>
    public static string Describe(string? slot)
    {
        if (string.IsNullOrWhiteSpace(slot))
        {
            return "Save";
        }

        var parts = slot.Split(':');

        // A state is emulator:core:slot, where core may be empty (SaveStateMatch.SlotKey).
        if (parts.Length == 3 && parts[1] != "battery")
        {
            return parts[2] == "auto" ? "Autosave state" : $"Save state {parts[2]}";
        }

        return parts switch
        {
            [_, "battery"] => "Battery save",
            [_, "battery", var qualifier] => $"Battery save ({qualifier})",
            [_, "gci"] => "Memory card",
            [_, "nand"] => "Console memory",
            [_, "nvram"] => "Machine memory",
            [_, "savedata"] => "Save data",
            [var only] => $"Save ({only})",
            _ => $"Save ({slot[(slot.IndexOf(':', StringComparison.Ordinal) + 1)..]})",
        };
    }
}
