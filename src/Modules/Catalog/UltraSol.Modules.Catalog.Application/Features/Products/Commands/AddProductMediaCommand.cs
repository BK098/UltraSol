using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Products.Commands;

public sealed record AddProductMediaDto(string Name);
public sealed record AddProductMediaCommand(AddProductMediaDto Model) : ICommand<ApiResult<object>>;
public sealed class AddProductMediaValidator : AbstractValidator<AddProductMediaCommand>
{
    public AddProductMediaValidator()
    {
    }
}
internal sealed class AddProductMediaCommandHandler : ICommandHandler<AddProductMediaCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(AddProductMediaCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}