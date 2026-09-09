using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Products.Commands;

public sealed record DiscardDraftProductDto(string Name);
public sealed record DiscardDraftProductCommand(DiscardDraftProductDto Model) : ICommand<ApiResult<object>>;
public sealed class DiscardDraftProductValidator : AbstractValidator<DiscardDraftProductCommand>
{
    public DiscardDraftProductValidator()
    {
    }
}
internal sealed class DiscardDraftProductCommandHandler : ICommandHandler<DiscardDraftProductCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(DiscardDraftProductCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}