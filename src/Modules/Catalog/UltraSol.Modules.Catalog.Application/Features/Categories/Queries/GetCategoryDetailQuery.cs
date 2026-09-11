using FluentValidation;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;
using static UltraSol.Modules.Catalog.Application.Features.Categories.Queries.GetCategoryDetailQuery;

namespace UltraSol.Modules.Catalog.Application.Features.Categories.Queries;

public sealed record GetCategoryDetailQuery(Guid CategoryId) : IQuery<ApiResult<GetCategoryDetailQuery.Response>>
{
    public sealed record Response(Guid Id, string Name, string? Description, bool IsArchived, Named? Parent);
    public sealed record Named(Guid Id, string Name);
}

public sealed class GetCategoryDetailValidator : AbstractValidator<GetCategoryDetailQuery>
{
    public GetCategoryDetailValidator()
    {
        RuleFor(x => x.CategoryId)
            .NotEmpty();
    }
}

internal sealed class GetCategoryDetailQueryHandler(ICatalogReadStore reads) : IQueryHandler<GetCategoryDetailQuery, ApiResult<GetCategoryDetailQuery.Response>>
{
    public async Task<ApiResult<GetCategoryDetailQuery.Response>> Handle(GetCategoryDetailQuery request, CancellationToken ct)
    {
        var data = await reads.CategoryAsync(request.CategoryId, ct);
        var row = data;
        return ApiResultBuilder.Success(new Response(row.Id, row.Name, row.Description, row.IsArchived,
            row.Parent is null ? null : new Named(row.Parent.Id, row.Parent.Name)));
    }
}