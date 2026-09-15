using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Variations.Commands;

public sealed record RemoveVariationCommand(Guid ProductId, Guid VariationId) : ICommand<ApiResult<object>>;
public sealed class RemoveVariationValidator : AbstractValidator<RemoveVariationCommand>
{
    public RemoveVariationValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty();
        RuleFor(x => x.VariationId)
            .NotEmpty();
    }
}
internal sealed class RemoveVariationCommandHandler(IProductRepository products, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<RemoveVariationCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RemoveVariationCommand request, CancellationToken cancellationToken) => unitOfWork.ExecuteInTransactionAsync(async ct =>
    {
        var product = await products.GetTrackedRequiredAsync(request.ProductId, ct);
        product.RemoveVariation(request.VariationId);
        return ApiResultBuilder.Success<object>(request.ProductId);
    }, cancellationToken);
}