namespace RomM.Client;

/// <summary>How an authenticated call ended.</summary>
public enum RomMResponseStatus
{
    Ok,

    /// <summary>
    /// 401. Expected, not exceptional: an expiring token is the recommended default for a
    /// portable install. Keep the local database and outbox, drop to pairing, resume after
    /// re-pairing on the same <c>client_device_identifier</c>.
    /// </summary>
    Unauthorized,

    /// <summary>403. The token is valid but was not granted the scope this call needs.</summary>
    Forbidden,

    /// <summary>
    /// 416. The resume point named a byte the server's copy does not have.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="ServerError"/> because it is the one refusal that says
    /// something about the local file rather than about the server: retrying with the same
    /// partial file is refused identically every time, so the file has to go.
    /// </remarks>
    RangeNotSatisfiable,

    /// <summary>Any other status the server returned.</summary>
    ServerError,
}

/// <summary>
/// The result of an authenticated call, which never throws on 401 or 403.
/// </summary>
/// <remarks>
/// Transport failures still throw <see cref="RomMUnreachableException"/>: not reaching the
/// server and being refused by it need different handling, and collapsing them into one
/// result type would hide that.
/// </remarks>
public sealed record RomMResponse<T>(RomMResponseStatus Status, T? Value, string? Message)
{
    public bool IsSuccess => Status == RomMResponseStatus.Ok;

    /// <summary>True when the stored token is expired or revoked and re-pairing is the answer.</summary>
    public bool NeedsRepairing => Status == RomMResponseStatus.Unauthorized;
}

/// <summary>Constructors for <see cref="RomMResponse{T}"/>.</summary>
public static class RomMResponse
{
    public static RomMResponse<T> Success<T>(T value) => new(RomMResponseStatus.Ok, value, null);

    public static RomMResponse<T> Failure<T>(RomMResponseStatus status, string message) =>
        new(status, default, message);
}
