using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Collections.Commands
{
    public sealed record RenameCollectionDto(string Name);
    public sealed record RenameCollectionCommand(RenameCollectionDto Model) : ICommand<ApiResult<object>>;
    public sealed class RenameCollectionValidator : AbstractValidator<RenameCollectionCommand>
    {
        public RenameCollectionValidator()
        {
        }
    }
    internal sealed class RenameCollectionCommandHandler : ICommandHandler<RenameCollectionCommand, ApiResult<object>>
    {
        public Task<ApiResult<object>> Handle(RenameCollectionCommand request, CancellationToken cancellationToken)
        {
            var model = request.Model;
            throw new NotImplementedException();
        }
    }
}