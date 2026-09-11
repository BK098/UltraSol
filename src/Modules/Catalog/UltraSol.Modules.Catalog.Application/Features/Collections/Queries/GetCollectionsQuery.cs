using FluentValidation;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;
using static UltraSol.Modules.Catalog.Application.Features.Collections.Queries.GetCollectionsQuery;

namespace UltraSol.Modules.Catalog.Application.Features.Collections.Queries;

public sealed record GetCollectionsQuery(PagedFilter? Filter, CollectionStatus? Status = null,
    CollectionType? Type = null) : IQuery<ApiResult<PaginatedResult<GetCollectionsQuery.Response>>>
{
    public sealed record Response(Guid Id, string Name, string Type, string Status);
}

public sealed class GetCollectionsValidator : AbstractValidator<GetCollectionsQuery>
{
    public GetCollectionsValidator()
    {
        RuleFor(x => x.Status!.Value)
            .IsInEnum()
            .When(x => x.Status.HasValue);
        RuleFor(x => x.Type!.Value)
            .IsInEnum()
            .When(x => x.Type.HasValue);
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

internal sealed class GetCollectionsQueryHandler(ICatalogReadStore reads) : IQueryHandler<GetCollectionsQuery, ApiResult<PaginatedResult<GetCollectionsQuery.Response>>>
{
    public async Task<ApiResult<PaginatedResult<GetCollectionsQuery.Response>>> Handle(GetCollectionsQuery request, CancellationToken ct)
    {
        var page = CatalogQueryPaging.Create(request.Filter!, "Name");
        var data = await reads.CollectionsAsync(page, request.Status, request.Type, ct);
        return ApiResultBuilder.Success(data.Map(row => new Response(row.Id, row.Name, row.Type.ToString(), row.Status.ToString())));
    }
}