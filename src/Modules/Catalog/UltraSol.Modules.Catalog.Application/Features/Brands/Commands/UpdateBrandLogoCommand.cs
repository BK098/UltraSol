using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Brands.Commands;

public sealed record UpdateBrandLogoDto(string Name);
public sealed record UpdateBrandLogoCommand(UpdateBrandLogoDto Model) : ICommand<ApiResult<object>>;
public sealed class UpdateBrandLogoValidator : AbstractValidator<UpdateBrandLogoCommand>
{
    public UpdateBrandLogoValidator()
    {
    }
}
internal sealed class UpdateBrandLogoCommandHandler : ICommandHandler<UpdateBrandLogoCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(UpdateBrandLogoCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}