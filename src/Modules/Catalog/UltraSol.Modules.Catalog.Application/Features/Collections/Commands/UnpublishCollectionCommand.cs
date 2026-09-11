using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Collections.Commands;

public sealed record UnpublishCollectionCommand(Guid CollectionId) : ICommand<ApiResult<object>>;

public sealed class UnpublishCollectionValidator : AbstractValidator<UnpublishCollectionCommand>
{
    public UnpublishCollectionValidator()
    {
        RuleFor(x => x.CollectionId)
            .NotEmpty();
    }
}

internal sealed class UnpublishCollectionCommandHandler(ICollectionRepository collections, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<UnpublishCollectionCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(UnpublishCollectionCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var collection = await collections.GetTrackedRequiredAsync(request.CollectionId, ct);
            collection.Unpublish();
            return ApiResultBuilder.Success<object>(collection.Id);
        }, cancellationToken);
}