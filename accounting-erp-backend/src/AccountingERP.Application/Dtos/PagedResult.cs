namespace AccountingERP.Application.Dtos;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static PagedResult<T> Empty(int page, int pageSize)
        => new(Array.Empty<T>(), page, pageSize, 0);
}

/// <summary>Normalised paging input. Guards against missing/absurd query-string values.</summary>
public readonly record struct PageRequest
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public int Page { get; }
    public int PageSize { get; }

    public PageRequest(int page, int pageSize)
    {
        Page = page < 1 ? 1 : page;
        PageSize = pageSize is < 1 or > MaxPageSize ? DefaultPageSize : pageSize;
    }

    public int Skip => (Page - 1) * PageSize;
    public int Take => PageSize;
}
