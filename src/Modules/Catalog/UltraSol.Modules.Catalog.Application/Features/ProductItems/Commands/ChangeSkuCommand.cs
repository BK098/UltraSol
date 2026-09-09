using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.ProductItems.Commands;

public sealed record ChangeSkuDto(string Name);
public sealed record ChangeSkuCommand(ChangeSkuDto Model) : ICommand<ApiResult<object>>;
public sealed class ChangeSkuValidator : AbstractValidator<ChangeSkuCommand>
{
    public ChangeSkuValidator()
    {
    }
}
internal sealed class ChangeSkuCommandHandler : ICommandHandler<ChangeSkuCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ChangeSkuCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}