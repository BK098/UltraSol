using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.ProductItems.Commands;

public sealed record ArchiveProductItemDto(string Name);
public sealed record ArchiveProductItemCommand(ArchiveProductItemDto Model) : ICommand<ApiResult<object>>;
public sealed class ArchiveProductItemValidator : AbstractValidator<ArchiveProductItemCommand>
{
    public ArchiveProductItemValidator()
    {
    }
}
internal sealed class ArchiveProductItemCommandHandler : ICommandHandler<ArchiveProductItemCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ArchiveProductItemCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}