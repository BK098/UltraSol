using FluentValidation;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;
using static UltraSol.Modules.Catalog.Application.Features.Variations.Queries.GetVariationOptionsQuery;

namespace UltraSol.Modules.Catalog.Application.Features.Variations.Queries;

public sealed record GetVariationOptionsQuery(Guid ProductId, Guid VariationId,
    PagedFilter? Filter) : IQuery<ApiResult<PaginatedResult<GetVariationOptionsQuery.Response>>>
{
    public sealed record Response(Guid Id, string Value, VariationReference Variation);
    public sealed record VariationReference(Guid Id, string Name, Named Product);
    public sealed record Named(Guid Id, string Name);
}

public sealed class GetVariationOptionsValidator : AbstractValidator<GetVariationOptionsQuery>
{
    public GetVariationOptionsValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty();
        RuleFor(x => x.VariationId)
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

internal sealed class GetVariationOptionsQueryHandler(ICatalogReadStore reads) : IQueryHandler<GetVariationOptionsQuery,
    ApiResult<PaginatedResult<GetVariationOptionsQuery.Response>>>
{
    public async Task<ApiResult<PaginatedResult<GetVariationOptionsQuery.Response>>> Handle(GetVariationOptionsQuery request, CancellationToken ct)
    {
        var page = CatalogQueryPaging.Create(request.Filter!, "Name");
        var owner = await reads.VariationAsync(request.ProductId, request.VariationId, ct);
        var data = CatalogQueryPaging.Children(owner.Options, page, x => x.Value);
        return ApiResultBuilder.Success(data.Map(row => new Response(row.Id, row.Value, new VariationReference(owner.Id, owner.Name,
            new Named(owner.Product.Id, owner.Product.Name)))));
    }
}