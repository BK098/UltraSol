using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Categories.Commands;

public sealed record ArchiveCategoryCommand(Guid CategoryId) : ICommand<ApiResult<object>>;
public sealed class ArchiveCategoryValidator : AbstractValidator<ArchiveCategoryCommand>
{
    public ArchiveCategoryValidator()
    {
    }
}
internal sealed class ArchiveCategoryCommandHandler(ICategoryRepository categories, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<ArchiveCategoryCommand, ApiResult<object>>
{
    public async Task<ApiResult<object>> Handle(ArchiveCategoryCommand request, CancellationToken cancellationToken)
    {
        var categoryId = request.CategoryId;
        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var category = await categories.GetTrackedRequiredAsync(categoryId, ct);
            category.Archive();
            return ApiResultBuilder.Success<object>("Category archived successfully");
        }, cancellationToken);
    }
}