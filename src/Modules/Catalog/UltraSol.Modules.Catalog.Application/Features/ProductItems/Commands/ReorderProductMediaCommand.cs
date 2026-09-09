using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.ProductItems.Commands;

public sealed record ReorderProductMediaDto(string Name);
public sealed record ReorderProductMediaCommand(ReorderProductMediaDto Model) : ICommand<ApiResult<object>>;
public sealed class ReorderProductMediaValidator : AbstractValidator<ReorderProductMediaCommand>
{
    public ReorderProductMediaValidator()
    {
    }
}
internal sealed class ReorderProductMediaCommandHandler : ICommandHandler<ReorderProductMediaCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ReorderProductMediaCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}