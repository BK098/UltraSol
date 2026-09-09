using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Collections.Commands;

public sealed record ReorderCollectionProductsDto(string Name);
public sealed record ReorderCollectionProductsCommand(ReorderCollectionProductsDto Model) : ICommand<ApiResult<object>>;
public sealed class ReorderCollectionProductsValidator : AbstractValidator<ReorderCollectionProductsCommand>
{
    public ReorderCollectionProductsValidator()
    {
    }
}
internal sealed class ReorderCollectionProductsCommandHandler : ICommandHandler<ReorderCollectionProductsCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ReorderCollectionProductsCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}