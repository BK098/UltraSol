using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Bundles.Commands;

public sealed record AddBundleComponentDto(int Quantity);
public sealed record AddBundleComponentCommand(Guid ProductId, Guid ProductItemId, Guid ComponentItemId, AddBundleComponentDto? Model) : ICommand<ApiResult<object>>;

public sealed class AddBundleComponentValidator : AbstractValidator<AddBundleComponentCommand>
{
    public AddBundleComponentValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty();
        RuleFor(x => x.ProductItemId)
            .NotEmpty();
        RuleFor(x => x.ComponentItemId)
            .NotEmpty();
        RuleFor(x => x.Model)
            .NotNull();
        RuleFor(x => x.Model!.Quantity)
            .GreaterThan(0)
            .When(x => x.Model is not null);
    }
}

internal sealed class AddBundleComponentCommandHandler(IProductRepository products, IProductItemRepository items, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<AddBundleComponentCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(AddBundleComponentCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var product = await products.GetTrackedRequiredAsync(request.ProductId, ct);
            var bundle = await items.GetTrackedRequiredAsync(request.ProductItemId, ct);
            var component = await items.GetRequiredByIdAsync(request.ComponentItemId, ct);
            bundle.AddBundleComponent(product, component, request.Model!.Quantity);
            return ApiResultBuilder.Success<object>(bundle.Id);
        }, cancellationToken);
}