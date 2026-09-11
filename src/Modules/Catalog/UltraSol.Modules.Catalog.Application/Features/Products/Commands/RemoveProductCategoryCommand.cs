using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Products.Commands;

public sealed record RemoveProductCategoryCommand(Guid ProductId, Guid CategoryId) : ICommand<ApiResult<object>>;

public sealed class RemoveProductCategoryValidator : AbstractValidator<RemoveProductCategoryCommand>
{
    public RemoveProductCategoryValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.CategoryId).NotEmpty();
    }
}

internal sealed class RemoveProductCategoryCommandHandler(IProductRepository products, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<RemoveProductCategoryCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RemoveProductCategoryCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var product = await products.GetTrackedRequiredAsync(request.ProductId, ct);
            product.RemoveCategory(request.CategoryId);
            return ApiResultBuilder.Success<object>(product.Id);
        }, cancellationToken);
}
