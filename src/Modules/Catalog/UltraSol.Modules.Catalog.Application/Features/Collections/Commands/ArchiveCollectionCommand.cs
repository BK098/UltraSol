using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Collections.Commands;

public sealed record ArchiveCollectionCommand(Guid CollectionId) : ICommand<ApiResult<object>>;

public sealed class ArchiveCollectionValidator : AbstractValidator<ArchiveCollectionCommand>
{
    public ArchiveCollectionValidator()
    {
        RuleFor(x => x.CollectionId)
            .NotEmpty();
    }
}

internal sealed class ArchiveCollectionCommandHandler(ICollectionRepository collections, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<ArchiveCollectionCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ArchiveCollectionCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var collection = await collections.GetTrackedRequiredAsync(request.CollectionId, ct);
            collection.Archive();
            return ApiResultBuilder.Success<object>(collection.Id);
        }, cancellationToken);
}