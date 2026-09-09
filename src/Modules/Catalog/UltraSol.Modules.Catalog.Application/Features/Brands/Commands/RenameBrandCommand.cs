using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Brands.Commands;

public sealed record RenameBrandDto(string Name);
public sealed record RenameBrandCommand(RenameBrandDto Model) : ICommand<ApiResult<object>>;
public sealed class RenameBrandValidator : AbstractValidator<RenameBrandCommand>
{
    public RenameBrandValidator()
    {
    }
}
internal sealed class RenameBrandCommandHandler : ICommandHandler<RenameBrandCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RenameBrandCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}