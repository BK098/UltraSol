using FluentValidation;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;
using static UltraSol.Modules.Catalog.Application.Features.ProductItems.Queries.GetProductItemMediaDetailQuery;

namespace UltraSol.Modules.Catalog.Application.Features.ProductItems.Queries;

public sealed record GetProductItemMediaDetailQuery(Guid ProductId, Guid ProductItemId, Guid MediaId) : IQuery<ApiResult<GetProductItemMediaDetailQuery.Response>>
{
    public sealed record Response(Guid Id, string Url, string? AltText, int SortOrder, bool IsPrimary, ItemReference ProductItem);
    public sealed record ItemReference(Guid Id, string Sku, Named Product);
    public sealed record Named(Guid Id, string Name);
}

public sealed class GetProductItemMediaDetailValidator : AbstractValidator<GetProductItemMediaDetailQuery>
{
    public GetProductItemMediaDetailValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty();
        RuleFor(x => x.ProductItemId)
            .NotEmpty();
        RuleFor(x => x.MediaId)
            .NotEmpty();
    }
}

internal sealed class GetProductItemMediaDetailQueryHandler(ICatalogReadStore reads) : IQueryHandler<GetProductItemMediaDetailQuery,
    ApiResult<GetProductItemMediaDetailQuery.Response>>
{
    public async Task<ApiResult<GetProductItemMediaDetailQuery.Response>> Handle(GetProductItemMediaDetailQuery request, CancellationToken ct)
    {
        var owner = await reads.ItemAsync(request.ProductItemId, request.ProductId, false, ct);
        var row = owner.Media.SingleOrDefault(x => x.Id == request.MediaId)
            ?? throw EntityNotFoundException.For<ProductItemMedia>(request.MediaId);
        return ApiResultBuilder.Success(new Response(row.Id, row.Url, row.AltText, row.SortOrder, row.IsPrimary, new ItemReference(owner.Id, owner.Sku,
            new Named(owner.Product.Id, owner.Product.Name))));
    }
}