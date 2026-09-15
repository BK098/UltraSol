using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Products.Commands;

public sealed record AssignProductCategoryCommand(Guid ProductId, Guid CategoryId) : ICommand<ApiResult<object>>;

public sealed class AssignProductCategoryValidator : AbstractValidator<AssignProductCategoryCommand>
{
    public AssignProductCategoryValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.CategoryId).NotEmpty();
    }
}

internal sealed class AssignProductCategoryCommandHandler(
    IProductRepository products,
    ICategoryRepository categories,
    ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<AssignProductCategoryCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(AssignProductCategoryCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var product = await products.GetTrackedRequiredAsync(request.ProductId, ct);
            var category = await categories.GetRequiredByIdAsync(request.CategoryId, ct);
            product.AddCategory(category);
            return ApiResultBuilder.Success<object>(product.Id);
        }, cancellationToken);
}