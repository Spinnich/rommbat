namespace RomMBat.Core.Content;

/// <summary>
/// What the volume the RetroBat tree sits on can and cannot do.
/// </summary>
/// <remarks>
/// A portable RetroBat often lives on exFAT or FAT32, and the differences are not cosmetic.
/// M0 probe 7 measured all of this on real sticks rather than reading it off a table.
/// <para>
/// <b>FAT32's 4 GB ceiling announces itself as the wrong error.</b> The write fails with
/// Win32 112 <c>ERROR_DISK_FULL</c>, "There is not enough space on the disk", on a volume with
/// 14.6 GB free. That message must never reach a user: it sends them to delete files that are
/// not the problem. So the size is compared here, before the transfer starts.
/// </para>
/// <para>
/// <b>exFAT is no gentler than FAT32 on timestamps</b>: 2 seconds on both, despite exFAT's
/// format allowing 10 ms, and the rounding is <b>up</b>, so a file can carry a timestamp later
/// than the clock that wrote it. That is why content hashes decide whether a file is the right
/// one and mtime is only ever a tiebreak.
/// </para>
/// </remarks>
public sealed record FilesystemLimits
{
    /// <summary>FAT32 refuses a file of 4 GiB or more, so the largest it holds is one byte short.</summary>
    public const long Fat32MaximumFileSize = (4L * 1024 * 1024 * 1024) - 1;

    /// <summary>What FAT32 and exFAT both round modification times to, measured.</summary>
    public static readonly TimeSpan CoarseTimestampGranularity = TimeSpan.FromSeconds(2);

    /// <summary>The volume's format as Windows reports it, or <c>unknown</c>.</summary>
    public required string Format { get; init; }

    /// <summary>The largest file this volume can hold, or null when nothing constrains it.</summary>
    public long? MaximumFileSizeBytes { get; init; }

    /// <summary>How coarsely this volume stores modification times.</summary>
    public TimeSpan TimestampGranularity { get; init; }

    /// <summary>How much room is left, at the moment this was taken.</summary>
    public long AvailableFreeBytes { get; init; }

    /// <summary>True when the format was read rather than guessed at.</summary>
    public bool IsKnown => !string.Equals(Format, "unknown", StringComparison.OrdinalIgnoreCase);

    /// <summary>Nothing is constrained. What a test uses, and what an unreadable volume falls back to.</summary>
    public static FilesystemLimits Unconstrained { get; } = new()
    {
        Format = "unknown",
        MaximumFileSizeBytes = null,
        TimestampGranularity = TimeSpan.Zero,
        AvailableFreeBytes = long.MaxValue,
    };

    /// <summary>
    /// Reads the limits of the volume holding a path.
    /// </summary>
    /// <remarks>
    /// Never throws. A path on a network share or a volume Windows will not describe answers
    /// <see cref="Unconstrained"/>, because refusing to sync on the strength of a failed probe
    /// would be worse than attempting the write and reporting what actually happened.
    /// </remarks>
    public static FilesystemLimits Inspect(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root))
            {
                return Unconstrained;
            }

            var drive = new DriveInfo(root);
            if (!drive.IsReady)
            {
                return Unconstrained;
            }

            return For(drive.DriveFormat, drive.AvailableFreeSpace);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException
            or NotSupportedException or System.Security.SecurityException)
        {
            return Unconstrained;
        }
    }

    /// <summary>Builds the limits for a named format, which is how a test reaches FAT32 without one.</summary>
    public static FilesystemLimits For(string format, long availableFreeBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        var isFat32 = format.Equals("FAT32", StringComparison.OrdinalIgnoreCase)
            || format.Equals("FAT", StringComparison.OrdinalIgnoreCase);
        var isCoarse = isFat32 || format.Equals("exFAT", StringComparison.OrdinalIgnoreCase);

        return new FilesystemLimits
        {
            Format = format,
            MaximumFileSizeBytes = isFat32 ? Fat32MaximumFileSize : null,
            TimestampGranularity = isCoarse ? CoarseTimestampGranularity : TimeSpan.Zero,
            AvailableFreeBytes = availableFreeBytes,
        };
    }

    /// <summary>True when a file of this size can exist here at all.</summary>
    public bool CanHold(long sizeBytes) =>
        MaximumFileSizeBytes is not { } maximum || sizeBytes <= maximum;

    /// <summary>
    /// True when two timestamps are indistinguishable on this volume.
    /// </summary>
    /// <remarks>
    /// On FAT32 and exFAT anything inside one 2-second window is the same stored value, so
    /// ordering by mtime inside that window is meaningless rather than merely imprecise.
    /// </remarks>
    public bool AreTimestampsEquivalent(DateTimeOffset left, DateTimeOffset right)
    {
        var difference = (left - right).Duration();
        return difference <= (TimestampGranularity > TimeSpan.Zero ? TimestampGranularity : TimeSpan.FromSeconds(1));
    }
}
