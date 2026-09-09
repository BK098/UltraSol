using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Bundles.Commands;

public sealed record ChangeBundleComponentQuantityDto(string Name);
public sealed record ChangeBundleComponentQuantityCommand(ChangeBundleComponentQuantityDto Model) : ICommand<ApiResult<object>>;
public sealed class ChangeBundleComponentQuantityValidator : AbstractValidator<ChangeBundleComponentQuantityCommand>
{
    public ChangeBundleComponentQuantityValidator()
    {
    }
}
internal sealed class ChangeBundleComponentQuantityCommandHandler : ICommandHandler<ChangeBundleComponentQuantityCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ChangeBundleComponentQuantityCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}