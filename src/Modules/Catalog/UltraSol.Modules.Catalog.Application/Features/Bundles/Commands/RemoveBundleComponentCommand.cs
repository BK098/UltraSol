using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Bundles.Commands;

public sealed record RemoveBundleComponentDto(string Name);
public sealed record RemoveBundleComponentCommand(RemoveBundleComponentDto Model) : ICommand<ApiResult<object>>;
public sealed class RemoveBundleComponentValidator : AbstractValidator<RemoveBundleComponentCommand>
{
    public RemoveBundleComponentValidator()
    {
    }
}
internal sealed class RemoveBundleComponentCommandHandler : ICommandHandler<RemoveBundleComponentCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RemoveBundleComponentCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}