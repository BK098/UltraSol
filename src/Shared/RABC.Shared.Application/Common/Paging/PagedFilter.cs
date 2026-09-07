namespace Domain.Common.Paging
{
    public sealed record PagedFilter
    {
        public int PageIndex { get; init; } = 1;
        public int PageSize { get; init; } = 10;
        public string? Search { get; init; }
    }
}