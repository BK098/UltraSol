using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using FluentValidation;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;
using static UltraSol.Modules.Catalog.Application.Features.ProductItems.Queries.GetProductItemsQuery;

namespace UltraSol.Modules.Catalog.Application.Features.ProductItems.Queries;

public sealed record GetProductItemsQuery(Guid ProductId, PagedFilter? Filter, ProductItemStatus? Status = null,
    bool? IsBundle = null) : IQuery<ApiResult<PaginatedResult<GetProductItemsQuery.Response>>>
{
    public sealed record Response(Guid Id, string Sku, string Status, Named Product, bool IsBundle, string? PrimaryMediaUrl, IReadOnlyList<Selection> Selections);
    public sealed record Named(Guid Id, string Name);
    public sealed record Selection(Guid VariationId, string VariationName, Guid OptionId, string OptionValue);
}

public sealed class GetProductItemsValidator : AbstractValidator<GetProductItemsQuery>
{
    public GetProductItemsValidator()
    {
        RuleFor(x => x.ProductId)
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

internal sealed class GetProductItemsQueryHandler(ICatalogReadStore reads) : IQueryHandler<GetProductItemsQuery,
    ApiResult<PaginatedResult<GetProductItemsQuery.Response>>>
{
    public async Task<ApiResult<PaginatedResult<GetProductItemsQuery.Response>>> Handle(GetProductItemsQuery request, CancellationToken ct)
    {
        var page = CatalogQueryPaging.Create(request.Filter!, "Sku");
        var data = await reads.ItemsAsync(page, new(request.ProductId, request.Status, request.IsBundle), ct);
        return ApiResultBuilder.Success(data.Map(row => new Response(row.Id, row.Sku, row.Status.ToString(), new Named(row.Product.Id, row.Product.Name),
            row.IsBundle, row.PrimaryMediaUrl, row.Selections.Select(v0 => new Selection(v0.VariationId, v0.VariationName, v0.OptionId, v0.OptionValue)).ToArray())));
    }
}