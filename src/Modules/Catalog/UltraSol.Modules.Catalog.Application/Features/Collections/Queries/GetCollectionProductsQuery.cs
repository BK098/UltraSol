using FluentValidation;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;
using static UltraSol.Modules.Catalog.Application.Features.Collections.Queries.GetCollectionProductsQuery;

namespace UltraSol.Modules.Catalog.Application.Features.Collections.Queries;

public sealed record GetCollectionProductsQuery(Guid CollectionId, PagedFilter? Filter,
    ProductStatus? Status = null) : IQuery<ApiResult<PaginatedResult<GetCollectionProductsQuery.Response>>>
{
    public sealed record Response(Guid Id, string Name, string Status, Named? Brand, string? PrimaryMediaUrl, IReadOnlyList<Named> Categories);
    public sealed record Named(Guid Id, string Name);
}

public sealed class GetCollectionProductsValidator : AbstractValidator<GetCollectionProductsQuery>
{
    public GetCollectionProductsValidator()
    {
        RuleFor(x => x.CollectionId)
            .NotEmpty();
        RuleFor(x => x.Status!.Value)
            .IsInEnum()
            .When(x => x.Status.HasValue);
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

internal sealed class GetCollectionProductsQueryHandler(ICatalogReadStore reads) : IQueryHandler<GetCollectionProductsQuery,
    ApiResult<PaginatedResult<GetCollectionProductsQuery.Response>>>
{
    public async Task<ApiResult<PaginatedResult<GetCollectionProductsQuery.Response>>> Handle(GetCollectionProductsQuery request, CancellationToken ct)
    {
        var page = CatalogQueryPaging.Create(request.Filter!, "Name");
        var data = await reads.ProductsAsync(page, new(Status: request.Status, CollectionId: request.CollectionId), ct);
        return ApiResultBuilder.Success(data.Map(row => new Response(row.Id, row.Name, row.Status.ToString(),
            row.Brand is null ? null : new Named(row.Brand.Id, row.Brand.Name), row.PrimaryMediaUrl, row.Categories.Select(v0 => new Named(v0.Id, v0.Name)).ToArray())));
    }
}