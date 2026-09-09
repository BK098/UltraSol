using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Categories.Commands;

public sealed record CreateCategoryDto(string Name);
public sealed record CreateCategoryCommand(CreateCategoryDto Model) : ICommand<ApiResult<object>>;
public sealed class CreateCategoryValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryValidator()
    {
    }
}
internal sealed class CreateCategoryCommandHandler : ICommandHandler<CreateCategoryCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}