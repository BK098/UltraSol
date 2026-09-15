using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Catalog.Categories;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Repositories;

namespace UltraSol.Modules.Catalog.Application.Features.Categories.Commands;

public sealed record UpdateCategoryDto(string Name, string? Description);
public sealed record UpdateCategoryCommand(Guid CategoryId, UpdateCategoryDto Model) : ICommand<ApiResult<object>>;
public sealed class UpdateCategoryValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryValidator()
    {
        RuleFor(x => x.Model.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters")
            .WithName("Name");
        RuleFor(x => x.Model.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters")
            .WithName("Description");
    }
}
internal sealed class UpdateCategoryCommandHandler(ICategoryRepository categories, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<UpdateCategoryCommand, ApiResult<object>>
{
    public async Task<ApiResult<object>> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        var categoryName = model.Name.Trim();
        var category = await categories.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category == null)
        {
            return ApiResultBuilder.NotFound<object>("Category not found");
        }
        if (!string.Equals(category.Name, categoryName, StringComparison.OrdinalIgnoreCase))
        {
            if (await categories.AnyAsync(c => string.Equals(c.Name, categoryName, StringComparison.OrdinalIgnoreCase), cancellationToken))
            {
                return ApiResultBuilder.Existed<object>("A category with the same name already exists.");
            }
        }
        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            category.Rename(categoryName);
            category.ChangeDescription(model.Description?.Trim());
            await categories.UpdateAsync(category, ct);
            return ApiResultBuilder.Success<object>("Category updated successfully", statusCode: 200);
        }, cancellationToken);
    }
}