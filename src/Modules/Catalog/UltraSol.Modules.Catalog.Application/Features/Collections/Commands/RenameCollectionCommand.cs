using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Collections.Commands;

public sealed record RenameCollectionDto(string? Name);
public sealed record RenameCollectionCommand(Guid CollectionId, RenameCollectionDto? Model) : ICommand<ApiResult<object>>;

public sealed class RenameCollectionValidator : AbstractValidator<RenameCollectionCommand>
{
    public RenameCollectionValidator()
    {
        RuleFor(x => x.CollectionId)
            .NotEmpty();
        RuleFor(x => x.Model)
            .NotNull();
        RuleFor(x => x.Model!.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required.")
            .When(x => x.Model is not null);
    }
}

internal sealed class RenameCollectionCommandHandler(ICollectionRepository collections, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<RenameCollectionCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RenameCollectionCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var collection = await collections.GetTrackedRequiredAsync(request.CollectionId, ct);
            collection.Rename(request.Model!.Name!);
            return ApiResultBuilder.Success<object>(collection.Id);
        }, cancellationToken);
}