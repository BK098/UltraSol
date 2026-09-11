//using FluentValidation;
//using UltraSol.Modules.Catalog.Domain.Abstractions;
//using UltraSol.Modules.Catalog.Domain.Repositories;
//using UltraSol.Shared.Application.Messaging.Commands;
//using UltraSol.Shared.Application.Responses;

//namespace UltraSol.Modules.Catalog.Application.Features.Categories.Commands;

//public sealed record DeactivateCategoryCommand(Guid CategoryId) : ICommand<ApiResult<object>>;
//public sealed class DeactivateCategoryValidator : AbstractValidator<DeactivateCategoryCommand>
//{
//    public DeactivateCategoryValidator()
//    {
//    }
//}
//internal sealed class DeactivateCategoryCommandHandler(ICategoryRepository categories, ICatalogUnitOfWork unitOfWork)
//    : ICommandHandler<DeactivateCategoryCommand, ApiResult<object>>
//{
//    public async Task<ApiResult<object>> Handle(DeactivateCategoryCommand request, CancellationToken cancellationToken)
//    {
//        var categoryId = request.CategoryId;
//        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
//        {
//            var category = await categories.GetTrackedRequiredAsync(categoryId, ct);
//            category.Deactivate();
//            return ApiResultBuilder.Success<object>("Category deactivated successfully");
//        }, cancellationToken);
//    }
//}