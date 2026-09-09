using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Collections.Commands;

public sealed record ArchiveCollectionDto(string Name);
public sealed record ArchiveCollectionCommand(ArchiveCollectionDto Model) : ICommand<ApiResult<object>>;
public sealed class ArchiveCollectionValidator : AbstractValidator<ArchiveCollectionCommand>
{
    public ArchiveCollectionValidator()
    {
    }
}
internal sealed class ArchiveCollectionCommandHandler : ICommandHandler<ArchiveCollectionCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ArchiveCollectionCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}