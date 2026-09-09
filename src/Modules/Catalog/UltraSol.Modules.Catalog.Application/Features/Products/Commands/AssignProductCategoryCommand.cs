using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Products.Commands;

public sealed record AssignProductCategoryDto(string Name, string Description);
public sealed record AssignProductCategoryCommand(AssignProductCategoryDto Model) : ICommand<ApiResult<object>>;
public sealed class AssignProductCategoryValidator : AbstractValidator<AssignProductCategoryCommand>
{
    public AssignProductCategoryValidator()
    {
    }
}
internal sealed class AssignProductCategoryCommandHandler : ICommandHandler<AssignProductCategoryCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(AssignProductCategoryCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}