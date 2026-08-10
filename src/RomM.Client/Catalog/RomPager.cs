namespace RomM.Client.Catalog;

/// <summary>
/// Walks <c>GET /api/roms</c> a page at a time, and can be stopped and resumed.
/// </summary>
/// <remarks>
/// Page size and resumption both come from M0 probe 5, measured against an 83,131 ROM
/// library. Latency is close to linear in page size at roughly 10 ms per ROM, so a bigger
/// page buys almost no throughput while costing responsiveness and making resumption
/// coarser. 250 gives a 2.5 s page and 333 pages for that library, and a full walk of about
/// 14 minutes.
/// <para>
/// A walk that long cannot be assumed to finish, so this class holds the offset rather than
/// a loop holding it. The caller writes <see cref="Offset"/> to <c>sync_cursor</c> after each
/// page, and a later run starts a new pager at the recorded offset.
/// </para>
/// <para>
/// Nothing accumulates here. One page is in memory at a time, which is what keeps the client
/// from ever holding the library.
/// </para>
/// </remarks>
public sealed class RomPager
{
    /// <summary>The measured default. Not the server's default, which is 50.</summary>
    public const int DefaultPageSize = 250;

    /// <summary>The server rejects anything larger.</summary>
    public const int MaximumPageSize = 10_000;

    private readonly RomMConnection _connection;

    public RomPager(RomMConnection connection, CatalogQuery query, int pageSize = DefaultPageSize, int startOffset = 0)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, MaximumPageSize);
        ArgumentOutOfRangeException.ThrowIfNegative(startOffset);

        _connection = connection;
        Query = query;
        PageSize = pageSize;
        Offset = startOffset;
    }

    /// <summary>The query being walked.</summary>
    public CatalogQuery Query { get; }

    public int PageSize { get; }

    /// <summary>The offset the next page starts at. Persist this to resume.</summary>
    public int Offset { get; private set; }

    /// <summary>How many rows the query matches, once a page has been read.</summary>
    public int? Total { get; private set; }

    /// <summary>How many rows this pager has yielded.</summary>
    public int Read { get; private set; }

    /// <summary>How many pages it has fetched.</summary>
    public int Pages { get; private set; }

    /// <summary>True once the walk has reached the end.</summary>
    public bool IsComplete { get; private set; }

    /// <summary>
    /// Fetches the next page and advances the offset.
    /// </summary>
    /// <remarks>
    /// The offset only moves on success, so a failed or interrupted call leaves the pager
    /// exactly where it was and the same page is fetched again next time.
    /// </remarks>
    public async Task<RomMResponse<RomPage>> NextAsync(CancellationToken cancellationToken = default)
    {
        if (IsComplete)
        {
            return RomMResponse.Success(new RomPage { Limit = PageSize, Offset = Offset });
        }

        var response = await _connection
            .GetRomPageAsync(Query, PageSize, Offset, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccess)
        {
            return response;
        }

        var page = response.Value!;
        Total = page.Total;
        Pages++;
        Read += page.Items.Count;
        Offset += page.Items.Count;

        // An empty page ends the walk even when total disagrees, which is what stops a short
        // count from looping forever against a library that shrank mid-walk.
        IsComplete = page.Items.Count == 0 || Offset >= page.Total;

        return response;
    }

    /// <summary>
    /// Yields every row in the walk.
    /// </summary>
    /// <remarks>
    /// Stops at the first failure and reports it through <paramref name="failure"/> rather
    /// than throwing, because a 401 is an expected state for a portable install and half a
    /// walk is still worth keeping.
    /// </remarks>
    public async IAsyncEnumerable<RomRow> ReadAllAsync(
        Action<RomMResponse<RomPage>> failure,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(failure);

        while (!IsComplete)
        {
            var response = await NextAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccess)
            {
                failure(response);
                yield break;
            }

            foreach (var row in response.Value!.Items)
            {
                yield return row;
            }
        }
    }
}
