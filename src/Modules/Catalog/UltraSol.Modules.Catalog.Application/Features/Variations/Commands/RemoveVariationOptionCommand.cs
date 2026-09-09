using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Variations.Commands;

public sealed record RemoveVariationOptionDto(string Name);
public sealed record RemoveVariationOptionCommand(RemoveVariationOptionDto Model) : ICommand<ApiResult<object>>;
public sealed class RemoveVariationOptionValidator : AbstractValidator<RemoveVariationOptionCommand>
{
    public RemoveVariationOptionValidator()
    {
    }
}
internal sealed class RemoveVariationOptionCommandHandler : ICommandHandler<RemoveVariationOptionCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RemoveVariationOptionCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}