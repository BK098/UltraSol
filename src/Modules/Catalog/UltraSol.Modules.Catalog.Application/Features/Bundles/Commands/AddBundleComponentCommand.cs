using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Bundles.Commands;

public sealed record AddBundleComponentDto(string Name);
public sealed record AddBundleComponentCommand(AddBundleComponentDto Model) : ICommand<ApiResult<object>>;
public sealed class AddBundleComponentValidator : AbstractValidator<AddBundleComponentCommand>
{
    public AddBundleComponentValidator()
    {
    }
}
internal sealed class AddBundleComponentCommandHandler : ICommandHandler<AddBundleComponentCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(AddBundleComponentCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}