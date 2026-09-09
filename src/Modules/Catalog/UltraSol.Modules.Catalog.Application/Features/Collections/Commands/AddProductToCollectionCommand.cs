using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Collections.Commands;

public sealed record AddProductToCollectionDto(string Name);
public sealed record AddProductToCollectionCommand(AddProductToCollectionDto Model) : ICommand<ApiResult<object>>;
public sealed class AddProductToCollectionValidator : AbstractValidator<AddProductToCollectionCommand>
{
    public AddProductToCollectionValidator()
    {
    }
}
internal sealed class AddProductToCollectionCommandHandler : ICommandHandler<AddProductToCollectionCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(AddProductToCollectionCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}