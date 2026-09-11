using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.ProductItems.Commands;

public sealed record ActivateProductItemCommand(Guid ProductItemId) : ICommand<ApiResult<object>>;
public sealed class ActivateProductItemValidator : AbstractValidator<ActivateProductItemCommand>
{
    public ActivateProductItemValidator()
    {
        RuleFor(x => x.ProductItemId).NotEmpty();
    }
}
internal sealed class ActivateProductItemCommandHandler(IProductItemRepository items, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<ActivateProductItemCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ActivateProductItemCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var item = await items.GetTrackedRequiredAsync(request.ProductItemId, ct);
            item.Activate();
            return ApiResultBuilder.Success<object>(item.Id);
        }, cancellationToken);
}