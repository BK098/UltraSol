using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Variations.Commands;

public sealed record AddVariationOptionDto(string Name);
public sealed record AddVariationOptionCommand(AddVariationOptionDto Model) : ICommand<ApiResult<object>>;
public sealed class AddVariationOptionValidator : AbstractValidator<AddVariationOptionCommand>
{
    public AddVariationOptionValidator()
    {
    }
}
internal sealed class AddVariationOptionCommandHandler : ICommandHandler<AddVariationOptionCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(AddVariationOptionCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}