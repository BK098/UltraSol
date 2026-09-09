using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.ProductItems.Commands;

public sealed record ActivateProductItemDto(string Name);
public sealed record ActivateProductItemCommand(ActivateProductItemDto Model) : ICommand<ApiResult<object>>;
public sealed class ActivateProductItemValidator : AbstractValidator<ActivateProductItemCommand>
{
    public ActivateProductItemValidator()
    {
    }
}
internal sealed class ActivateProductItemCommandHandler : ICommandHandler<ActivateProductItemCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ActivateProductItemCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}