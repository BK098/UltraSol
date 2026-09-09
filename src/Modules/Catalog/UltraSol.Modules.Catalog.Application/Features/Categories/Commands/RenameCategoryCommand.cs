using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Categories.Commands;

public sealed record RenameCategoryDto(string Name);
public sealed record RenameCategoryCommand(RenameCategoryDto Model) : ICommand<ApiResult<object>>;
public sealed class RenameCategoryValidator : AbstractValidator<RenameCategoryCommand>
{
    public RenameCategoryValidator()
    {
    }
}
internal sealed class RenameCategoryCommandHandler : ICommandHandler<RenameCategoryCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RenameCategoryCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}