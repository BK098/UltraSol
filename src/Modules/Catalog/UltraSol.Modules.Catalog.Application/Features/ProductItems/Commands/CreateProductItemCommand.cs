using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems.ValueObjects;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.ProductItems.Commands;

public sealed record ProductItemSelectionDto(Guid VariationId, Guid OptionId);
public sealed record CreateProductItemDto(string? Sku, IReadOnlyList<ProductItemSelectionDto>? Selections);
public sealed record CreateProductItemCommand(Guid ProductId, CreateProductItemDto? Model) : ICommand<ApiResult<object>>;
public sealed class CreateProductItemValidator : AbstractValidator<CreateProductItemCommand>
{
    public CreateProductItemValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Model).NotNull();
        RuleFor(x => x.Model!.Sku).Must(sku => !string.IsNullOrWhiteSpace(sku))
            .WithMessage("Sku is required.")
            .When(x => x.Model is not null);
        RuleForEach(x => x.Model!.Selections!).ChildRules(selection =>
        {
            selection.RuleFor(x => x.VariationId).NotEmpty();
            selection.RuleFor(x => x.OptionId).NotEmpty();
        }).When(x => x.Model?.Selections is not null);
    }
}
internal sealed class CreateProductItemCommandHandler(IProductRepository products, IProductItemRepository items, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<CreateProductItemCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(CreateProductItemCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var product = await products.GetTrackedRequiredAsync(request.ProductId, ct);
            var sku = SKU.Create(request.Model!.Sku!);
            if (await items.ExistsBySkuAsync(sku.Value, ct))
            {
                return ApiResultBuilder.Existed<object>("SKU");
            }
            var selections = request.Model.Selections?.Select(x => new OptionSelection(x.VariationId, x.OptionId)).ToArray() ?? [];
            var item = ProductItem.Create(product, sku, selections);
            await items.AddAsync(item, ct);
            return ApiResultBuilder.Success<object>(item.Id, statusCode: 201);
        }, cancellationToken);
}