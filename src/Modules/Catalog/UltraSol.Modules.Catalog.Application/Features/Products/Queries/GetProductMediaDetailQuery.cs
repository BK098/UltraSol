using FluentValidation;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;
using static UltraSol.Modules.Catalog.Application.Features.Products.Queries.GetProductMediaDetailQuery;

namespace UltraSol.Modules.Catalog.Application.Features.Products.Queries;

public sealed record GetProductMediaDetailQuery(Guid ProductId, Guid MediaId) : IQuery<ApiResult<GetProductMediaDetailQuery.Response>>
{
    public sealed record Response(Guid Id, string Url, string? AltText, int SortOrder, bool IsPrimary, Named Product);
    public sealed record Named(Guid Id, string Name);
}

public sealed class GetProductMediaDetailValidator : AbstractValidator<GetProductMediaDetailQuery>
{
    public GetProductMediaDetailValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty();
        RuleFor(x => x.MediaId)
            .NotEmpty();
    }
}

internal sealed class GetProductMediaDetailQueryHandler(ICatalogReadStore reads) : IQueryHandler<GetProductMediaDetailQuery,
    ApiResult<GetProductMediaDetailQuery.Response>>
{
    public async Task<ApiResult<GetProductMediaDetailQuery.Response>> Handle(GetProductMediaDetailQuery request, CancellationToken ct)
    {
        var owner = await reads.ProductAsync(request.ProductId, ct);
        var row = owner.Media.SingleOrDefault(x => x.Id == request.MediaId)
            ?? throw EntityNotFoundException.For<ProductMedia>(request.MediaId);
        return ApiResultBuilder.Success(new Response(row.Id, row.Url, row.AltText, row.SortOrder, row.IsPrimary, new Named(owner.Id, owner.Name)));
    }
}