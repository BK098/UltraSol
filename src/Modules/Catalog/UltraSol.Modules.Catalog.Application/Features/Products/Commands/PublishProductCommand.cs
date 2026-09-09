using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Abstractions;
using UltraSol.Shared.Domain.Common.Repositories;

namespace UltraSol.Modules.Catalog.Application.Features.Products.Commands;

public sealed record PublishProductCommand(Guid ProductId) : ICommand<ApiResult<object>>;

public sealed class PublishProductValidator : AbstractValidator<PublishProductCommand>
{
    public PublishProductValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
    }
}

internal sealed class PublishProductCommandHandler(IProductRepository products, ICatalogUnitOfWork unitOfWork) : ICommandHandler<PublishProductCommand, ApiResult<object>>
{
    public async Task<ApiResult<object>> Handle(PublishProductCommand request, CancellationToken cancellationToken)
    {
        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var product = await products.GetTrackedRequiredAsync(request.ProductId, ct);
            product.Publish();
            return ApiResultBuilder.Success<object>(product.Id);
        }, cancellationToken);
    }
}

public sealed class ProductPublishedHandler : IDomainEventHandler
{
    public Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}