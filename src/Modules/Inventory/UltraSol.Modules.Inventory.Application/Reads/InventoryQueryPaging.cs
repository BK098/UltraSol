using FluentValidation;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Inventory.Application.Reads;

internal static class InventoryQueryPaging
{
    internal static PaginationRequest Create(PagedFilter filter) => new() { PageNumber = filter.PageIndex, PageSize = filter.PageSize, Search = filter.Search };

    internal static void AddRules<T>(AbstractValidator<T> validator, Func<T, PagedFilter?> filter)
    {
        validator.RuleFor(request => filter(request)).NotNull().WithName("Filter");
        validator.RuleFor(request => filter(request)!.PageIndex)
            .GreaterThan(0)
            .When(request => filter(request) is not null)
            .WithName("Filter.PageIndex");
        validator.RuleFor(request => filter(request)!.PageSize)
            .InclusiveBetween(1, PaginationRequest.MaxPageSize)
            .When(request => filter(request) is not null)
            .WithName("Filter.PageSize");
    }
}
