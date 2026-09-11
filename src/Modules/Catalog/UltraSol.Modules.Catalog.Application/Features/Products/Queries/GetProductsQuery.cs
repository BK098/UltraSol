using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using FluentValidation;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;
using static UltraSol.Modules.Catalog.Application.Features.Products.Queries.GetProductsQuery;

namespace UltraSol.Modules.Catalog.Application.Features.Products.Queries;

public sealed record GetProductsQuery(PagedFilter? Filter, ProductStatus? Status = null, Guid? BrandId = null,
    Guid? CategoryId = null) : IQuery<ApiResult<PaginatedResult<GetProductsQuery.Response>>>
{
    public sealed record Response(Guid Id, string Name, string Status, Named? Brand, string? PrimaryMediaUrl, IReadOnlyList<Named> Categories);
    public sealed record Named(Guid Id, string Name);
}

public sealed class GetProductsValidator : AbstractValidator<GetProductsQuery>
{
    public GetProductsValidator()
    {
        RuleFor(x => x.Status!.Value)
            .IsInEnum()
            .When(x => x.Status.HasValue);
        RuleFor(x => x.BrandId)
            .NotEqual(Guid.Empty)
            .When(x => x.BrandId.HasValue);
        RuleFor(x => x.CategoryId)
            .NotEqual(Guid.Empty)
            .When(x => x.CategoryId.HasValue);
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

internal sealed class GetProductsQueryHandler(ICatalogReadStore reads) : IQueryHandler<GetProductsQuery, ApiResult<PaginatedResult<GetProductsQuery.Response>>>
{
    public async Task<ApiResult<PaginatedResult<GetProductsQuery.Response>>> Handle(GetProductsQuery request, CancellationToken ct)
    {
        var page = CatalogQueryPaging.Create(request.Filter!, "Name");
        var data = await reads.ProductsAsync(page, new(Status: request.Status, BrandId: request.BrandId, CategoryId: request.CategoryId), ct);
        return ApiResultBuilder.Success(data.Map(row => new Response(row.Id, row.Name, row.Status.ToString(),
            row.Brand is null ? null : new Named(row.Brand.Id, row.Brand.Name), row.PrimaryMediaUrl, row.Categories.Select(v0 => new Named(v0.Id, v0.Name)).ToArray())));
    }
}