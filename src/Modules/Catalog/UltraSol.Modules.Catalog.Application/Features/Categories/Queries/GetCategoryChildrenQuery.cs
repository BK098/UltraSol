using FluentValidation;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;
using static UltraSol.Modules.Catalog.Application.Features.Categories.Queries.GetCategoryChildrenQuery;

namespace UltraSol.Modules.Catalog.Application.Features.Categories.Queries;

public sealed record GetCategoryChildrenQuery(Guid CategoryId, PagedFilter? Filter,
    bool? IsArchived = null) : IQuery<ApiResult<PaginatedResult<GetCategoryChildrenQuery.Response>>>
{
    public sealed record Response(Guid Id, string Name, bool IsArchived, Named? Parent);
    public sealed record Named(Guid Id, string Name);
}

public sealed class GetCategoryChildrenValidator : AbstractValidator<GetCategoryChildrenQuery>
{
    public GetCategoryChildrenValidator()
    {
        RuleFor(x => x.CategoryId)
            .NotEmpty();
        RuleFor(x => x.Filter)
            .NotNull();
        RuleFor(x => x.Filter!.PageIndex)
            .GreaterThan(0)
            .When(x => x.Filter is not null);
        RuleFor(x => x.Filter!.PageSize)
            .InclusiveBetween(1, PaginationRequest.MaxPageSize)
            .When(x => x.Filter is not null);
    }
}

internal sealed class GetCategoryChildrenQueryHandler(ICatalogReadStore reads) : IQueryHandler<GetCategoryChildrenQuery,
    ApiResult<PaginatedResult<GetCategoryChildrenQuery.Response>>>
{
    public async Task<ApiResult<PaginatedResult<GetCategoryChildrenQuery.Response>>> Handle(GetCategoryChildrenQuery request, CancellationToken ct)
    {
        var page = CatalogQueryPaging.Create(request.Filter!, "Name");
        var data = await reads.CategoriesAsync(page, request.CategoryId, false, request.IsArchived, ct);
        return ApiResultBuilder.Success(data.Map(row => new Response(row.Id, row.Name, row.IsArchived, row.Parent is null ? null : new Named(row.Parent.Id,
            row.Parent.Name))));
    }
}