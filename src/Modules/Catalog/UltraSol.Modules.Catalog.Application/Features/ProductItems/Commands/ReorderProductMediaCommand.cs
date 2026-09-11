using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.ProductItems.Commands;

public sealed record ReorderProductMediaDto(IReadOnlyList<Guid>? MediaIds);
public sealed record ReorderProductMediaCommand(Guid ProductId, Guid ProductItemId, ReorderProductMediaDto? Model) : ICommand<ApiResult<object>>;
public sealed class ReorderProductMediaValidator : AbstractValidator<ReorderProductMediaCommand>
{
    public ReorderProductMediaValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.ProductItemId).NotEmpty();
        RuleFor(x => x.Model).NotNull();
        RuleFor(x => x.Model!.MediaIds)
            .NotNull().WithMessage("MediaIds is required.")
            .When(x => x.Model is not null);
        RuleForEach(x => x.Model!.MediaIds!)
            .NotEmpty()
            .When(x => x.Model?.MediaIds is not null);
    }
}
internal sealed class ReorderProductMediaCommandHandler(IProductRepository products, IProductItemRepository items, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<ReorderProductMediaCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ReorderProductMediaCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var product = await products.GetTrackedRequiredAsync(request.ProductId, ct);
            var item = await items.GetTrackedRequiredAsync(request.ProductItemId, ct);
            item.ReorderMedia(product, request.Model!.MediaIds!);
            return ApiResultBuilder.Success<object>(item.Id);
        }, cancellationToken);
}