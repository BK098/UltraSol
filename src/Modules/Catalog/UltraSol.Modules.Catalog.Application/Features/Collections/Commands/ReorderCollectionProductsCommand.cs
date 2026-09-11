using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Collections.Commands;

public sealed record ReorderCollectionProductsDto(IReadOnlyList<Guid>? ProductIds);
public sealed record ReorderCollectionProductsCommand(Guid CollectionId, ReorderCollectionProductsDto? Model) : ICommand<ApiResult<object>>;

public sealed class ReorderCollectionProductsValidator : AbstractValidator<ReorderCollectionProductsCommand>
{
    public ReorderCollectionProductsValidator()
    {
        RuleFor(x => x.CollectionId)
            .NotEmpty();
        RuleFor(x => x.Model)
            .NotNull();
        RuleFor(x => x.Model!.ProductIds)
            .NotNull()
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage("Product IDs must be unique.")
            .When(x => x.Model is not null);
        RuleForEach(x => x.Model!.ProductIds!)
            .NotEmpty()
            .When(x => x.Model?.ProductIds is not null);
    }
}

internal sealed class ReorderCollectionProductsCommandHandler(ICollectionRepository collections, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<ReorderCollectionProductsCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ReorderCollectionProductsCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var collection = await collections.GetTrackedRequiredAsync(request.CollectionId, ct);
            collection.ReorderProducts(request.Model!.ProductIds!);
            return ApiResultBuilder.Success<object>(collection.Id);
        }, cancellationToken);
}