using FluentValidation;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;
using static UltraSol.Modules.Catalog.Application.Features.Bundles.Queries.GetBundleComponentsQuery;

namespace UltraSol.Modules.Catalog.Application.Features.Bundles.Queries;

public sealed record GetBundleComponentsQuery(Guid ProductId, Guid ProductItemId,
    PagedFilter? Filter) : IQuery<ApiResult<PaginatedResult<GetBundleComponentsQuery.Response>>>
{
    public sealed record Response(Guid Id, string Sku, Named Product, string Status, int Quantity, string? PrimaryMediaUrl, ItemReference Bundle);
    public sealed record Named(Guid Id, string Name);
    public sealed record ItemReference(Guid Id, string Sku, Named Product);
}

public sealed class GetBundleComponentsValidator : AbstractValidator<GetBundleComponentsQuery>
{
    public GetBundleComponentsValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty();
        RuleFor(x => x.ProductItemId)
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

internal sealed class GetBundleComponentsQueryHandler(ICatalogReadStore reads) : IQueryHandler<GetBundleComponentsQuery,
    ApiResult<PaginatedResult<GetBundleComponentsQuery.Response>>>
{
    public async Task<ApiResult<PaginatedResult<GetBundleComponentsQuery.Response>>> Handle(GetBundleComponentsQuery request, CancellationToken ct)
    {
        var page = CatalogQueryPaging.Create(request.Filter!, "Name");
        var owner = await reads.ItemAsync(request.ProductItemId, request.ProductId, true, ct);
        var data = CatalogQueryPaging.Children(owner.Components, page, x => x.Sku + " " + x.Product.Name);
        return ApiResultBuilder.Success(data.Map(row => new Response(row.Id, row.Sku, new Named(row.Product.Id, row.Product.Name), row.Status,
            row.Quantity, row.PrimaryMediaUrl, new ItemReference(owner.Id, owner.Sku, new Named(owner.Product.Id, owner.Product.Name)))));
    }
}