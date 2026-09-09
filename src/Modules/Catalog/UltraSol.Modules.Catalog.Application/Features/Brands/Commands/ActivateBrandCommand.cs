using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Brands.Commands;

public sealed record ActivateBrandDto(string Name);
public sealed record ActivateBrandCommand(ActivateBrandDto Model) : ICommand<ApiResult<object>>;
public sealed class ActivateBrandValidator : AbstractValidator<ActivateBrandCommand>
{
    public ActivateBrandValidator()
    {
    }
}
internal sealed class ActivateBrandCommandHandler : ICommandHandler<ActivateBrandCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ActivateBrandCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}