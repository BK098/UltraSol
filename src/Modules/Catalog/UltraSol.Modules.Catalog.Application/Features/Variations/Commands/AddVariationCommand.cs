using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Variations.Commands;

public sealed record AddVariationCommandDto(string Name);
public sealed record AddVariationCommandCommand(AddVariationCommandDto Model) : ICommand<ApiResult<object>>;
public sealed class AddVariationCommandValidator : AbstractValidator<AddVariationCommandCommand>
{
    public AddVariationCommandValidator()
    {
    }
}
internal sealed class AddVariationCommandCommandHandler : ICommandHandler<AddVariationCommandCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(AddVariationCommandCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}