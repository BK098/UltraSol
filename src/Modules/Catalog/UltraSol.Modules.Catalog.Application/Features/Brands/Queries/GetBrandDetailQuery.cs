using FluentValidation;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;
using static UltraSol.Modules.Catalog.Application.Features.Brands.Queries.GetBrandDetailQuery;

namespace UltraSol.Modules.Catalog.Application.Features.Brands.Queries;

public sealed record GetBrandDetailQuery(Guid BrandId) : IQuery<ApiResult<GetBrandDetailQuery.Response>>
{
    public sealed record Response(Guid Id, string Name, string? Description, string? LogoUrl, bool IsActive, bool IsArchived);
}

public sealed class GetBrandDetailValidator : AbstractValidator<GetBrandDetailQuery>
{
    public GetBrandDetailValidator()
    {
        RuleFor(x => x.BrandId)
            .NotEmpty();
    }
}

internal sealed class GetBrandDetailQueryHandler(ICatalogReadStore reads) : IQueryHandler<GetBrandDetailQuery, ApiResult<GetBrandDetailQuery.Response>>
{
    public async Task<ApiResult<GetBrandDetailQuery.Response>> Handle(GetBrandDetailQuery request, CancellationToken ct)
    {
        var data = await reads.BrandAsync(request.BrandId, ct);
        var row = data;
        return ApiResultBuilder.Success(new Response(row.Id, row.Name, row.Description, row.LogoUrl, row.IsActive, row.IsArchived));
    }
}