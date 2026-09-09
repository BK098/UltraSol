using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Products.Commands;

public sealed record RemoveProductMediaDto(string Name);
public sealed record RemoveProductMediaCommand(RemoveProductMediaDto Model) : ICommand<ApiResult<object>>;
public sealed class RemoveProductMediaValidator : AbstractValidator<RemoveProductMediaCommand>
{
    public RemoveProductMediaValidator()
    {
    }
}
internal sealed class RemoveProductMediaCommandHandler : ICommandHandler<RemoveProductMediaCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RemoveProductMediaCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}