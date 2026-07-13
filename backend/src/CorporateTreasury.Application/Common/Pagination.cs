namespace CorporateTreasury.Application.Common;

/// <summary>
/// A page request. Normalizes out-of-range values (page ≥ 1, page size within a sane cap) so
/// callers cannot ask for page 0 or an unbounded page. First introduced by <c>ledger-entries</c>;
/// reused by later list/report endpoints.
/// </summary>
public sealed record PagedRequest(int Page = 1, int PageSize = 20)
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;

    public int NormalizedPage => Page < 1 ? 1 : Page;

    public int NormalizedPageSize =>
        PageSize < 1 ? DefaultPageSize : (PageSize > MaxPageSize ? MaxPageSize : PageSize);

    public int Skip => (NormalizedPage - 1) * NormalizedPageSize;

    public int Take => NormalizedPageSize;
}

/// <summary>A page of results plus the total count for the applied filter.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
