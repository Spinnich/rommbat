namespace RomM.Client;

/// <summary>
/// Everything a <see cref="RomMConnection"/> needs to reach one RomM instance.
/// </summary>
public sealed record RomMClientOptions
{
    /// <summary>
    /// The origin the user configured, for example <c>https://romm.example.lan</c>. Any path
    /// on it is preserved, so an instance behind a reverse-proxy subpath works.
    /// </summary>
    public required Uri Origin { get; init; }

    /// <summary>
    /// How long the TCP handshake may take.
    /// </summary>
    /// <remarks>
    /// M0 probe 6b: an absent host on the local subnet takes <b>21 seconds</b> to fail and a
    /// default <see cref="HttpClient"/> inherits every millisecond. This is the only lever
    /// that caps it; <see cref="RequestTimeout"/> bounds the response body too, so lowering
    /// that instead would abort legitimate large downloads.
    /// </remarks>
    public TimeSpan ConnectTimeout { get; init; } = InteractiveConnectTimeout;

    /// <summary>How long a whole request may take, body included.</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Sent as <c>User-Agent</c>, so a server operator can see which client is calling.</summary>
    public string UserAgent { get; init; } = "RomMBat";

    /// <summary>
    /// The <c>rmm_</c> token pairing returned, or null before pairing. Sent as a Bearer
    /// header, which is also why no cookie or CSRF handling exists anywhere in this client.
    /// </summary>
    public string? AccessToken { get; init; }

    /// <summary>
    /// The interactive budget from M0 probe 6b: orders of magnitude above LAN RTT (39 ms
    /// measured against a healthy instance), and inside the window where a spinner still
    /// reads as responsive.
    /// </summary>
    public static TimeSpan InteractiveConnectTimeout => TimeSpan.FromSeconds(2);

    /// <summary>
    /// For operations already known to be long-running, where a slower link is worth waiting
    /// out and no one is watching a spinner.
    /// </summary>
    public static TimeSpan BackgroundConnectTimeout => TimeSpan.FromSeconds(10);
}
