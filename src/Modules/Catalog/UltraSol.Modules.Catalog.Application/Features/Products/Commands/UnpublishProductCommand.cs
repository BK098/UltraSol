using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Products.Commands;

public sealed record UnpublishProductCommand(Guid ProductId) : ICommand<ApiResult<ProductResponse>>;

public sealed class UnpublishProductValidator : AbstractValidator<UnpublishProductCommand>
{
    public UnpublishProductValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
    }
}

internal sealed class UnpublishProductCommandHandler(IProductRepository products, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<UnpublishProductCommand, ApiResult<ProductResponse>>
{
    public async Task<ApiResult<ProductResponse>> Handle(UnpublishProductCommand request, CancellationToken cancellationToken)
    {
        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var product = await products.GetTrackedRequiredAsync(request.ProductId, ct);
            product.Unpublish();
            return ApiResultBuilder.Success(ProductResponse.From(product));
        }, cancellationToken);
    }
}