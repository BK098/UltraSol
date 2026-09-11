using FluentValidation;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;
using static UltraSol.Modules.Catalog.Application.Features.ProductItems.Queries.GetAllProductItemsQuery;

namespace UltraSol.Modules.Catalog.Application.Features.ProductItems.Queries;

public sealed record GetAllProductItemsQuery(PagedFilter? Filter, ProductItemStatus? Status = null,
    bool? IsBundle = null) : IQuery<ApiResult<PaginatedResult<GetAllProductItemsQuery.Response>>>
{
    public sealed record Response(Guid Id, string Sku, string Status, Named Product, bool IsBundle, string? PrimaryMediaUrl, IReadOnlyList<Selection> Selections);
    public sealed record Named(Guid Id, string Name);
    public sealed record Selection(Guid VariationId, string VariationName, Guid OptionId, string OptionValue);
}

public sealed class GetAllProductItemsValidator : AbstractValidator<GetAllProductItemsQuery>
{
    public GetAllProductItemsValidator()
    {
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

internal sealed class GetAllProductItemsQueryHandler(ICatalogReadStore reads) : IQueryHandler<GetAllProductItemsQuery,
    ApiResult<PaginatedResult<GetAllProductItemsQuery.Response>>>
{
    public async Task<ApiResult<PaginatedResult<GetAllProductItemsQuery.Response>>> Handle(GetAllProductItemsQuery request, CancellationToken ct)
    {
        var page = CatalogQueryPaging.Create(request.Filter!, "Sku");
        var data = await reads.ItemsAsync(page, new(Status: request.Status, IsBundle: request.IsBundle), ct);
        return ApiResultBuilder.Success(data.Map(row => new Response(row.Id, row.Sku, row.Status.ToString(), new Named(row.Product.Id, row.Product.Name),
            row.IsBundle, row.PrimaryMediaUrl, row.Selections.Select(v0 => new Selection(v0.VariationId, v0.VariationName, v0.OptionId, v0.OptionValue)).ToArray())));
    }
}