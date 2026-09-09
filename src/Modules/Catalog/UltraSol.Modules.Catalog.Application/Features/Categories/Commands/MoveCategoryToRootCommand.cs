using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Categories.Commands;

public sealed record MoveCategoryToRootDto(string Name);
public sealed record MoveCategoryToRootCommand(MoveCategoryToRootDto Model) : ICommand<ApiResult<object>>;
public sealed class MoveCategoryToRootValidator : AbstractValidator<MoveCategoryToRootCommand>
{
    public MoveCategoryToRootValidator()
    {
    }
}
internal sealed class MoveCategoryToRootCommandHandler : ICommandHandler<MoveCategoryToRootCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(MoveCategoryToRootCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}