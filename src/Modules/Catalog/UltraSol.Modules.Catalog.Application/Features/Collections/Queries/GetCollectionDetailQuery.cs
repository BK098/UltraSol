using FluentValidation;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;
using static UltraSol.Modules.Catalog.Application.Features.Collections.Queries.GetCollectionDetailQuery;

namespace UltraSol.Modules.Catalog.Application.Features.Collections.Queries;

public sealed record GetCollectionDetailQuery(Guid CollectionId) : IQuery<ApiResult<GetCollectionDetailQuery.Response>>
{
    public sealed record Response(Guid Id, string Name, string? Description, string Type, string Status, string? MatchMode, IReadOnlyList<Named> Brands,
        IReadOnlyList<Named> Categories);
    public sealed record Named(Guid Id, string Name);
}

public sealed class GetCollectionDetailValidator : AbstractValidator<GetCollectionDetailQuery>
{
    public GetCollectionDetailValidator()
    {
        RuleFor(x => x.CollectionId)
            .NotEmpty();
    }
}

internal sealed class GetCollectionDetailQueryHandler(ICatalogReadStore reads) : IQueryHandler<GetCollectionDetailQuery, ApiResult<GetCollectionDetailQuery.Response>>
{
    public async Task<ApiResult<GetCollectionDetailQuery.Response>> Handle(GetCollectionDetailQuery request, CancellationToken ct)
    {
        var data = await reads.CollectionAsync(request.CollectionId, ct);
        var row = data;
        return ApiResultBuilder.Success(new Response(row.Id, row.Name, row.Description, row.Type.ToString(), row.Status.ToString(),
            row.MatchMode?.ToString(), row.Brands.Select(v0 => new Named(v0.Id, v0.Name)).ToArray(), row.Categories.Select(v0 => new Named(v0.Id, v0.Name)).ToArray()));
    }
}