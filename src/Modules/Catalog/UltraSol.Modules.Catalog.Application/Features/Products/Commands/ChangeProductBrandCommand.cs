using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Products.Commands;

public sealed record ChangeProductBrandDto(string Name);
public sealed record ChangeProductBrandCommand(ChangeProductBrandDto Model) : ICommand<ApiResult<object>>;
public sealed class ChangeProductBrandValidator : AbstractValidator<ChangeProductBrandCommand>
{
    public ChangeProductBrandValidator()
    {
    }
}
internal sealed class ChangeProductBrandCommandHandler : ICommandHandler<ChangeProductBrandCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ChangeProductBrandCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}
