using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Brands.Commands;

public sealed record CreateBrandDto(string Name);
public sealed record CreateBrandCommand(CreateBrandDto Model) : ICommand<ApiResult<object>>;
public sealed class CreateBrandValidator : AbstractValidator<CreateBrandCommand>
{
    public CreateBrandValidator()
    {
    }
}
internal sealed class CreateBrandCommandHandler : ICommandHandler<CreateBrandCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(CreateBrandCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}