using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Catalog.Application.Reads;

internal static class CatalogQueryPaging
{
    internal static PaginationRequest Create(PagedFilter filter, string sort) =>
        PaginationRequest.Create(filter.PageIndex, filter.PageSize, SortDescriptor.Asc(sort), SortDescriptor.Asc("Id")) with { Search = filter.Search };

    // Child collections already belong to a single materialized owner and retain domain order.
    internal static PaginatedResult<T> Children<T>(IEnumerable<T> children, PaginationRequest page, Func<T, string> searchText)
    {
        var search = page.Search?.Trim();
        var rows = string.IsNullOrWhiteSpace(search)
            ? children.ToArray()
            : children.Where(x => searchText(x).Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
        return PaginatedResult<T>.Create(rows.Skip(page.Skip).Take(page.Take).ToArray(), rows.Length, page);
    }
}