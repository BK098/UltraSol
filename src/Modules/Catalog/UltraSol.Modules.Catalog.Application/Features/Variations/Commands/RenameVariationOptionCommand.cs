using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Variations.Commands;

public sealed record RenameVariationOptionDto(string Name);
public sealed record RenameVariationOptionCommand(RenameVariationOptionDto Model) : ICommand<ApiResult<object>>;
public sealed class RenameVariationOptionValidator : AbstractValidator<RenameVariationOptionCommand>
{
    public RenameVariationOptionValidator()
    {
    }
}
internal sealed class RenameVariationOptionCommandHandler : ICommandHandler<RenameVariationOptionCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RenameVariationOptionCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}