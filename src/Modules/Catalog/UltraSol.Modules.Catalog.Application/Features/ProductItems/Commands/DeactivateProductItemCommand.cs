using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.ProductItems.Commands;

public sealed record DeactivateProductItemCommand(Guid ProductItemId) : ICommand<ApiResult<object>>;
public sealed class DeactivateProductItemValidator : AbstractValidator<DeactivateProductItemCommand>
{
    public DeactivateProductItemValidator()
    {
        RuleFor(x => x.ProductItemId).NotEmpty();
    }
}
internal sealed class DeactivateProductItemCommandHandler(IProductItemRepository items, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<DeactivateProductItemCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(DeactivateProductItemCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var item = await items.GetTrackedRequiredAsync(request.ProductItemId, ct);
            item.Deactivate();
            return ApiResultBuilder.Success<object>(item.Id);
        }, cancellationToken);
}