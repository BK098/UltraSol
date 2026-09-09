using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.ProductItems.Commands;

public sealed record CreateProductItemDto(string Name);
public sealed record CreateProductItemCommand(CreateProductItemDto Model) : ICommand<ApiResult<object>>;
public sealed class CreateProductItemValidator : AbstractValidator<CreateProductItemCommand>
{
    public CreateProductItemValidator()
    {
    }
}
internal sealed class CreateProductItemCommandHandler : ICommandHandler<CreateProductItemCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(CreateProductItemCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}