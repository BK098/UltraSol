using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Collections.Commands;

public sealed record PublishCollectionCommand(Guid CollectionId) : ICommand<ApiResult<object>>;

public sealed class PublishCollectionValidator : AbstractValidator<PublishCollectionCommand>
{
    public PublishCollectionValidator()
    {
        RuleFor(x => x.CollectionId)
            .NotEmpty();
    }
}

internal sealed class PublishCollectionCommandHandler(ICollectionRepository collections, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<PublishCollectionCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(PublishCollectionCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var collection = await collections.GetTrackedRequiredAsync(request.CollectionId, ct);
            collection.Publish();
            return ApiResultBuilder.Success<object>(collection.Id);
        }, cancellationToken);
}