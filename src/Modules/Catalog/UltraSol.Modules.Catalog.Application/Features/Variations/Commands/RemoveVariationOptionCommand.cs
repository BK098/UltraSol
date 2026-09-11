using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Variations.Commands;

public sealed record RemoveVariationOptionCommand(Guid ProductId, Guid VariationId, Guid OptionId) : ICommand<ApiResult<object>>;
public sealed class RemoveVariationOptionValidator : AbstractValidator<RemoveVariationOptionCommand>
{
    public RemoveVariationOptionValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty();
        RuleFor(x => x.VariationId)
            .NotEmpty();
        RuleFor(x => x.OptionId)
            .NotEmpty();
    }
}
internal sealed class RemoveVariationOptionCommandHandler(IProductRepository products, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<RemoveVariationOptionCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RemoveVariationOptionCommand request, CancellationToken cancellationToken) => unitOfWork.ExecuteInTransactionAsync(async ct =>
    {
        var product = await products.GetTrackedRequiredAsync(request.ProductId, ct);
        product.RemoveVariationOption(request.VariationId, request.OptionId);
        return ApiResultBuilder.Success<object>(request.VariationId);
    }, cancellationToken);
}
