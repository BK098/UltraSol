using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Repositories;

namespace UltraSol.Modules.Catalog.Application.Features.Products.Commands;

public sealed record ArchiveProductCommand(Guid ProductId) : ICommand<ApiResult<object>>;
public sealed class ArchiveProductValidator : AbstractValidator<ArchiveProductCommand>
{
    public ArchiveProductValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
    }
}
internal sealed class ArchiveProductCommandHandler(IProductRepository products,ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<ArchiveProductCommand, ApiResult<object>>
{
    public async Task<ApiResult<object>> Handle(ArchiveProductCommand request, CancellationToken cancellationToken)
    {
        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var product = await products.GetTrackedRequiredAsync(request.ProductId, ct);
            product.Archive();
            return ApiResultBuilder.Success<object>("Product archived successfully");
        }, cancellationToken);
    }
}