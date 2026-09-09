using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Variations.Commands;

public sealed record RenameVariationDto(string Name);
public sealed record RenameVariationCommand(RenameVariationDto Model) : ICommand<ApiResult<object>>;
public sealed class RenameVariationValidator : AbstractValidator<RenameVariationCommand>
{
    public RenameVariationValidator()
    {
    }
}
internal sealed class RenameVariationCommandHandler : ICommandHandler<RenameVariationCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RenameVariationCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}