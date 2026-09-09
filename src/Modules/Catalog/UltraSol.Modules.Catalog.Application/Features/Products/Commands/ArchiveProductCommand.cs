using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Products.Commands;

public sealed record ArchiveProductDto(string Name);
public sealed record ArchiveProductCommand(ArchiveProductDto Model) : ICommand<ApiResult<object>>;
public sealed class ArchiveProductValidator : AbstractValidator<ArchiveProductCommand>
{
    public ArchiveProductValidator()
    {
    }
}
internal sealed class ArchiveProductCommandHandler : ICommandHandler<ArchiveProductCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ArchiveProductCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}