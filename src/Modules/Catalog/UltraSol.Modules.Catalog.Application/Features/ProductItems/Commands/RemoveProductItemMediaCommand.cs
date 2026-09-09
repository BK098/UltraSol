using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.ProductItems.Commands;

public sealed record RemoveProductItemMediaDto(string Name);
public sealed record RemoveProductItemMediaCommand(RemoveProductItemMediaDto Model) : ICommand<ApiResult<object>>;
public sealed class RemoveProductItemMediaValidator : AbstractValidator<RemoveProductItemMediaCommand>
{
    public RemoveProductItemMediaValidator()
    {
    }
}
internal sealed class RemoveProductItemMediaCommandHandler : ICommandHandler<RemoveProductItemMediaCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RemoveProductItemMediaCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}