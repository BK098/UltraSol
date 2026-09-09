using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.ProductItems.Commands;

public sealed record DeactivateProductItemDto(string Name);
public sealed record DeactivateProductItemCommand(DeactivateProductItemDto Model) : ICommand<ApiResult<object>>;
public sealed class DeactivateProductItemValidator : AbstractValidator<DeactivateProductItemCommand>
{
    public DeactivateProductItemValidator()
    {
    }
}
internal sealed class DeactivateProductItemCommandHandler : ICommandHandler<DeactivateProductItemCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(DeactivateProductItemCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}