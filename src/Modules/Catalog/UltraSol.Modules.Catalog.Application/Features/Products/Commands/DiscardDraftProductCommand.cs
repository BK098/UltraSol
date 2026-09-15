using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Catalog.Application.Features.Products.Commands;

public sealed record DiscardDraftProductCommand(Guid ProductId) : ICommand<ApiResult<object>>;

public sealed class DiscardDraftProductValidator : AbstractValidator<DiscardDraftProductCommand>
{
    public DiscardDraftProductValidator() => RuleFor(x => x.ProductId).NotEmpty();
}

internal sealed class DiscardDraftProductCommandHandler(IProductRepository products, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<DiscardDraftProductCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(DiscardDraftProductCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var product = await products.GetTrackedRequiredAsync(request.ProductId, ct);
            if (product.Status != ProductStatus.Draft)
            {
                throw new DomainException("Only draft products can be discarded.");
            }
            product.Archive();
            return ApiResultBuilder.Success<object>(product.Id);
        }, cancellationToken);
}