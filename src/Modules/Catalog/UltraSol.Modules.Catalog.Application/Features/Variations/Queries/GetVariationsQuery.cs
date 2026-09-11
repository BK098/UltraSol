using FluentValidation;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;
using static UltraSol.Modules.Catalog.Application.Features.Variations.Queries.GetVariationsQuery;

namespace UltraSol.Modules.Catalog.Application.Features.Variations.Queries;

public sealed record GetVariationsQuery(Guid ProductId, PagedFilter? Filter) : IQuery<ApiResult<PaginatedResult<GetVariationsQuery.Response>>>
{
    public sealed record Response(Guid Id, string Name, Named Product, int OptionCount);
    public sealed record Named(Guid Id, string Name);
}

public sealed class GetVariationsValidator : AbstractValidator<GetVariationsQuery>
{
    public GetVariationsValidator()
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

internal sealed class GetVariationsQueryHandler(ICatalogReadStore reads) : IQueryHandler<GetVariationsQuery, ApiResult<PaginatedResult<GetVariationsQuery.Response>>>
{
    public async Task<ApiResult<PaginatedResult<GetVariationsQuery.Response>>> Handle(GetVariationsQuery request, CancellationToken ct)
    {
        var page = CatalogQueryPaging.Create(request.Filter!, "Name");
        var data = await reads.VariationsAsync(request.ProductId, page, ct);
        return ApiResultBuilder.Success(data.Map(row => new Response(row.Id, row.Name, new Named(row.Product.Id, row.Product.Name), row.OptionCount)));
    }
}