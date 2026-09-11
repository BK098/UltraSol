using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Bundles.Commands;

public sealed record RemoveBundleComponentCommand(Guid ProductId, Guid ProductItemId, Guid ComponentItemId) : ICommand<ApiResult<object>>;

public sealed class RemoveBundleComponentValidator : AbstractValidator<RemoveBundleComponentCommand>
{
    public RemoveBundleComponentValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty();
        RuleFor(x => x.ProductItemId)
            .NotEmpty();
        RuleFor(x => x.ComponentItemId)
            .NotEmpty();
    }
}

internal sealed class RemoveBundleComponentCommandHandler(IProductRepository products, IProductItemRepository items, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<RemoveBundleComponentCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RemoveBundleComponentCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var product = await products.GetTrackedRequiredAsync(request.ProductId, ct);
            var bundle = await items.GetTrackedRequiredAsync(request.ProductItemId, ct);
            var component = await items.GetRequiredByIdAsync(request.ComponentItemId, ct);
            bundle.RemoveBundleComponent(product, component.Id);
            return ApiResultBuilder.Success<object>(bundle.Id);
        }, cancellationToken);
}