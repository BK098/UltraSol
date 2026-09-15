using FluentValidation;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;
using static UltraSol.Modules.Catalog.Application.Features.Brands.Queries.GetBrandsQuery;

namespace UltraSol.Modules.Catalog.Application.Features.Brands.Queries;

public sealed record GetBrandsQuery(PagedFilter? Filter, bool? IsActive = null, bool? IsArchived = null) : IQuery<ApiResult<PaginatedResult<Response>>>
{
    public sealed record Response(Guid Id, string Name, string? LogoUrl, bool IsActive, bool IsArchived);
}

public sealed class GetBrandsValidator : AbstractValidator<GetBrandsQuery>
{
    public GetBrandsValidator()
    {
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

internal sealed class GetBrandsQueryHandler(ICatalogReadStore reads) : IQueryHandler<GetBrandsQuery, ApiResult<PaginatedResult<Response>>>
{
    public async Task<ApiResult<PaginatedResult<Response>>> Handle(GetBrandsQuery request, CancellationToken ct)
    {
        var page = CatalogQueryPaging.Create(request.Filter!, "Name");
        var data = await reads.BrandsAsync(page, request.IsActive, request.IsArchived, ct);
        return ApiResultBuilder.Success(data.Map(row => new Response(row.Id, row.Name, row.LogoUrl, row.IsActive, row.IsArchived)));
    }
}