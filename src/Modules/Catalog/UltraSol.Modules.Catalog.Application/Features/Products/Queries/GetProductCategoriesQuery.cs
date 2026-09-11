using FluentValidation;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;
using static UltraSol.Modules.Catalog.Application.Features.Products.Queries.GetProductCategoriesQuery;

namespace UltraSol.Modules.Catalog.Application.Features.Products.Queries;

public sealed record GetProductCategoriesQuery(Guid ProductId, PagedFilter? Filter) : IQuery<ApiResult<PaginatedResult<GetProductCategoriesQuery.Response>>>
{
    public sealed record Response(Guid Id, string Name, Named Product);
    public sealed record Named(Guid Id, string Name);
}

public sealed class GetProductCategoriesValidator : AbstractValidator<GetProductCategoriesQuery>
{
    public GetProductCategoriesValidator()
    {
        RuleFor(x => x.ProductId)
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

internal sealed class GetProductCategoriesQueryHandler(ICatalogReadStore reads) : IQueryHandler<GetProductCategoriesQuery,
    ApiResult<PaginatedResult<GetProductCategoriesQuery.Response>>>
{
    public async Task<ApiResult<PaginatedResult<GetProductCategoriesQuery.Response>>> Handle(GetProductCategoriesQuery request, CancellationToken ct)
    {
        var page = CatalogQueryPaging.Create(request.Filter!, "Name");
        var owner = await reads.ProductAsync(request.ProductId, ct);
        var data = CatalogQueryPaging.Children(owner.Categories, page, x => x.Name);
        return ApiResultBuilder.Success(data.Map(row => new Response(row.Id, row.Name, new Named(owner.Id, owner.Name))));
    }
}