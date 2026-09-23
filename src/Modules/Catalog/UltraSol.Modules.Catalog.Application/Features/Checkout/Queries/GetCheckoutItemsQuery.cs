using FluentValidation;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Checkout.Queries;

public sealed record GetCheckoutItemsQuery(Guid[]? ProductItemIds) : IQuery<ApiResult<GetCheckoutItemsQuery.Response>>
{
    public sealed record Response(IReadOnlyList<Item> Items);
    public sealed record Item(Guid ProductItemId, Guid ProductId, string SkuCode, string ProductName,
        string VariantDescription, string? ImageUrl, bool IsSellable, string? ReasonCode, bool IsBundle,
        IReadOnlyList<Component> Components);
    public sealed record Component(Guid ProductItemId, int Quantity);
}

public sealed class GetCheckoutItemsValidator : AbstractValidator<GetCheckoutItemsQuery>
{
    public GetCheckoutItemsValidator()
    {
        RuleFor(request => request.ProductItemIds).NotNull().NotEmpty().Must(ids => ids is null || ids.Length <= 100)
            .WithMessage("At most 100 product items can be checked out at once.")
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Length).WithMessage("Product item IDs must be unique.");
        RuleForEach(request => request.ProductItemIds).NotEmpty();
    }
}

internal sealed class GetCheckoutItemsQueryHandler(IClientCatalogReadStore reads)
    : IQueryHandler<GetCheckoutItemsQuery, ApiResult<GetCheckoutItemsQuery.Response>>
{
    public async Task<ApiResult<GetCheckoutItemsQuery.Response>> Handle(GetCheckoutItemsQuery request, CancellationToken ct)
    {
        var rows = (await reads.CheckoutItemsAsync(request.ProductItemIds!, ct)).ToDictionary(row => row.ProductItemId);
        var items = request.ProductItemIds!.Select(id => rows.TryGetValue(id, out var row)
            ? new GetCheckoutItemsQuery.Item(row.ProductItemId, row.ProductId, row.SkuCode, row.ProductName,
                row.VariantDescription, row.ImageUrl, row.IsSellable, row.ReasonCode, row.IsBundle,
                row.Components.Select(component => new GetCheckoutItemsQuery.Component(component.ProductItemId, component.Quantity)).ToArray())
            : new GetCheckoutItemsQuery.Item(id, Guid.Empty, "", "", "", null, false, "ProductItemNotFound", false, [])).ToArray();
        return ApiResultBuilder.Success(new GetCheckoutItemsQuery.Response(items));
    }
}