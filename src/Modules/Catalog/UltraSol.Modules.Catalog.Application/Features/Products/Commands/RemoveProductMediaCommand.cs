using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Products.Commands;

public sealed record RemoveProductMediaCommand(Guid ProductId, Guid MediaId) : ICommand<ApiResult<object>>;

public sealed class RemoveProductMediaValidator : AbstractValidator<RemoveProductMediaCommand>
{
    public RemoveProductMediaValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.MediaId).NotEmpty();
    }
}

internal sealed class RemoveProductMediaCommandHandler(IProductRepository products, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<RemoveProductMediaCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RemoveProductMediaCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var product = await products.GetTrackedRequiredAsync(request.ProductId, ct);
            product.RemoveMedia(request.MediaId);
            return ApiResultBuilder.Success<object>(product.Id);
        }, cancellationToken);
}
