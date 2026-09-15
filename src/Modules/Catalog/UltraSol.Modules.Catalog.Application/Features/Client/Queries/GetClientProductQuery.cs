using FluentValidation;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;
namespace UltraSol.Modules.Catalog.Application.Features.Client.Queries;
public sealed record GetClientProductQuery(Guid ProductId) : IQuery<ApiResult<GetClientProductQuery.Response>>
{
    public sealed record Response(ProductResponse Product, IReadOnlyList<ItemResponse> Items);
    public sealed record ProductResponse(Guid Id, string Name, string? Description, string? Image);
    public sealed record ItemResponse(Guid Id, string Sku, string? Image);
}
public sealed class GetClientProductValidator : AbstractValidator<GetClientProductQuery>
{
    public GetClientProductValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
    }
}
internal sealed class GetClientProductQueryHandler(IClientCatalogReadStore reads) : IQueryHandler<GetClientProductQuery, ApiResult<GetClientProductQuery.Response>>
{
    public async Task<ApiResult<GetClientProductQuery.Response>> Handle(GetClientProductQuery request, CancellationToken ct)
    {
        var detail = await reads.ProductAsync(request.ProductId, ct);
        var product = detail.Product;
        return ApiResultBuilder.Success(new GetClientProductQuery.Response(
            new(product.Id, product.Name, product.Description, product.Image),
            detail.Items.Select(item => new GetClientProductQuery.ItemResponse(item.Id, item.Sku, item.Image)).ToArray()));
    }
}