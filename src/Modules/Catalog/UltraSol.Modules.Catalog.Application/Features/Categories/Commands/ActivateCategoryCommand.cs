using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Categories.Commands;

public sealed record ActivateCategoryDto(string Name);
public sealed record ActivateCategoryCommand(ActivateCategoryDto Model) : ICommand<ApiResult<object>>;
public sealed class ActivateCategoryValidator : AbstractValidator<ActivateCategoryCommand>
{
    public ActivateCategoryValidator()
    {
    }
}
internal sealed class ActivateCategoryCommandHandler : ICommandHandler<ActivateCategoryCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ActivateCategoryCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}