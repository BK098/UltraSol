using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Catalog.Categories;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Categories.Commands;

public sealed record CreateCategoryDto(string Name, string? Description);
public sealed record CreateCategoryCommand(CreateCategoryDto Model) : ICommand<ApiResult<object>>;
public sealed class CreateCategoryValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryValidator()
    {
        RuleFor(x => x.Model.Name)
            .NotEmpty()
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters")
            .WithName("Name");
        RuleFor(x => x.Model.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters")
            .WithName("Description");
    }
}
internal sealed class CreateCategoryCommandHandler(ICategoryRepository categories, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<CreateCategoryCommand, ApiResult<object>>
{
    public async Task<ApiResult<object>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        var categoryName = model.Name.Trim();
        if (await categories.FirstOrDefaultAsync(x => x.Name.Trim() == categoryName) != null)
        {
            return ApiResultBuilder.Existed<object>("A category with the same name already exists.");
        }
        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var product = Category.CreateRoot(model.Name!, model.Description);
            await categories.AddAsync(product, ct);
            return ApiResultBuilder.Success<object>("Category created successfully", statusCode: 201);
        }, cancellationToken);
    }
}