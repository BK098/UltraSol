using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.ProductItems.Commands;

public sealed record RemoveProductItemMediaCommand(Guid ProductId, Guid ProductItemId, Guid MediaId) : ICommand<ApiResult<object>>;
public sealed class RemoveProductItemMediaValidator : AbstractValidator<RemoveProductItemMediaCommand>
{
    public RemoveProductItemMediaValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.ProductItemId).NotEmpty();
        RuleFor(x => x.MediaId).NotEmpty();
    }
}
internal sealed class RemoveProductItemMediaCommandHandler(IProductRepository products, IProductItemRepository items, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<RemoveProductItemMediaCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RemoveProductItemMediaCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var product = await products.GetTrackedRequiredAsync(request.ProductId, ct);
            var item = await items.GetTrackedRequiredAsync(request.ProductItemId, ct);
            item.RemoveMedia(product, request.MediaId);
            return ApiResultBuilder.Success<object>(item.Id);
        }, cancellationToken);
}
