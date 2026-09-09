using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Variations.Commands;

public sealed record RemoveVariationDto(string Name);
public sealed record RemoveVariationCommand(RemoveVariationDto Model) : ICommand<ApiResult<object>>;
public sealed class RemoveVariationValidator : AbstractValidator<RemoveVariationCommand>
{
    public RemoveVariationValidator()
    {
    }
}
internal sealed class RemoveVariationCommandHandler : ICommandHandler<RemoveVariationCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RemoveVariationCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}