using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems.ValueObjects;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.ProductItems.Commands;

public sealed record ChangeSkuDto(string? Sku);
public sealed record ChangeSkuCommand(Guid ProductItemId, ChangeSkuDto? Model) : ICommand<ApiResult<object>>;
public sealed class ChangeSkuValidator : AbstractValidator<ChangeSkuCommand>
{
    public ChangeSkuValidator()
    {
        RuleFor(x => x.ProductItemId).NotEmpty();
        RuleFor(x => x.Model).NotNull();
        RuleFor(x => x.Model!.Sku)
            .Must(sku => !string.IsNullOrWhiteSpace(sku))
            .WithMessage("Sku is required.")
            .When(x => x.Model is not null);
    }
}
internal sealed class ChangeSkuCommandHandler(IProductItemRepository items, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<ChangeSkuCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ChangeSkuCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var item = await items.GetTrackedRequiredAsync(request.ProductItemId, ct);
            var sku = SKU.Create(request.Model!.Sku!);
            if (item.Sku != sku && await items.ExistsBySkuAsync(sku.Value, ct))
            {
                return ApiResultBuilder.Existed<object>("SKU");
            }
            item.ChangeSku(sku);
            return ApiResultBuilder.Success<object>(item.Id);
        }, cancellationToken);
}