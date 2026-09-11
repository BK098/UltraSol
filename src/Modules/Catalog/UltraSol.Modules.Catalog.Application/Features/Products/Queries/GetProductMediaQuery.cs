using FluentValidation;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;
using static UltraSol.Modules.Catalog.Application.Features.Products.Queries.GetProductMediaQuery;

namespace UltraSol.Modules.Catalog.Application.Features.Products.Queries;

public sealed record GetProductMediaQuery(Guid ProductId, PagedFilter? Filter) : IQuery<ApiResult<PaginatedResult<GetProductMediaQuery.Response>>>
{
    public sealed record Response(Guid Id, string Url, string? AltText, int SortOrder, bool IsPrimary, Named Product);
    public sealed record Named(Guid Id, string Name);
}

public sealed class GetProductMediaValidator : AbstractValidator<GetProductMediaQuery>
{
    public GetProductMediaValidator()
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

internal sealed class GetProductMediaQueryHandler(ICatalogReadStore reads) : IQueryHandler<GetProductMediaQuery,
    ApiResult<PaginatedResult<GetProductMediaQuery.Response>>>
{
    public async Task<ApiResult<PaginatedResult<GetProductMediaQuery.Response>>> Handle(GetProductMediaQuery request, CancellationToken ct)
    {
        var page = CatalogQueryPaging.Create(request.Filter!, "Name");
        var owner = await reads.ProductAsync(request.ProductId, ct);
        var data = CatalogQueryPaging.Children(owner.Media, page, x => x.AltText ?? x.Url);
        return ApiResultBuilder.Success(data.Map(row => new Response(row.Id, row.Url, row.AltText, row.SortOrder, row.IsPrimary, new Named(owner.Id, owner.Name))));
    }
}