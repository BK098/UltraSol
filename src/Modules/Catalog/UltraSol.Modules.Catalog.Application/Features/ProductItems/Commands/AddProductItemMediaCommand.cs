using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.ProductItems.Commands;

public sealed record AddProductItemMediaDto(string Name);
public sealed record AddProductItemMediaCommand(AddProductItemMediaDto Model) : ICommand<ApiResult<object>>;
public sealed class AddProductItemMediaValidator : AbstractValidator<AddProductItemMediaCommand>
{
    public AddProductItemMediaValidator()
    {
        //UNDONE: 
    }
}
internal sealed class AddProductItemMediaCommandHandler : ICommandHandler<AddProductItemMediaCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(AddProductItemMediaCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}