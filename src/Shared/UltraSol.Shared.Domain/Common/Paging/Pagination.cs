namespace UltraSol.Shared.Domain.Common.Paging
{
    public enum SortDirection
    {
        Ascending = 0,
        Descending = 1
    }

    public sealed record SortDescriptor(string Field, SortDirection Direction = SortDirection.Ascending)
    {
        public static SortDescriptor Asc(string field) => new(field, SortDirection.Ascending);
        public static SortDescriptor Desc(string field) => new(field, SortDirection.Descending);
    }

    public sealed record PaginationRequest
    {
        public const int DefaultPageSize = 20;
        public const int MaxPageSize = 200;

        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; } = DefaultPageSize;
        public IReadOnlyList<SortDescriptor> Sorts { get; init; } = [];
        public string? Search { get; init; }

        public int Skip => Math.Max(PageNumber - 1, 0) * Take;
        public int Take => Math.Clamp(PageSize, 1, MaxPageSize);

        public static PaginationRequest Default => new();

        public static PaginationRequest Create(int pageNumber, int pageSize, params SortDescriptor[] sorts) =>
            new()
            {
                PageNumber = pageNumber < 1 ? 1 : pageNumber,
                PageSize = pageSize,
                Sorts = sorts
            };
    }

    public sealed record PaginatedResult<T>
    {
        public required IReadOnlyList<T> Items { get; init; }
        public required int PageNumber { get; init; }
        public required int PageSize { get; init; }
        public required int TotalCount { get; init; }

        public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPrevious => PageNumber > 1;
        public bool HasNext => PageNumber < TotalPages;

        public static PaginatedResult<T> Empty(PaginationRequest page) => new()
        {
            Items = [],
            PageNumber = page.PageNumber,
            PageSize = page.Take,
            TotalCount = 0
        };

        public static PaginatedResult<T> Create(IReadOnlyList<T> items, int totalCount, PaginationRequest page) => new()
        {
            Items = items,
            PageNumber = page.PageNumber,
            PageSize = page.Take,
            TotalCount = totalCount
        };

        public PaginatedResult<TOut> Map<TOut>(Func<T, TOut> mapper) => new()
        {
            Items = Items.Select(mapper).ToList(),
            PageNumber = PageNumber,
            PageSize = PageSize,
            TotalCount = TotalCount
        };
    }
}
