using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Brands.Commands;

public sealed record DeactivateBrandDto(string Name);
public sealed record DeactivateBrandCommand(DeactivateBrandDto Model) : ICommand<ApiResult<object>>;
public sealed class DeactivateBrandValidator : AbstractValidator<DeactivateBrandCommand>
{
    public DeactivateBrandValidator()
    {
    }
}
internal sealed class DeactivateBrandCommandHandler : ICommandHandler<DeactivateBrandCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(DeactivateBrandCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}