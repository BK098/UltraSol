using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Collections.Commands;

public sealed record AddProductToCollectionCommand(Guid CollectionId, Guid ProductId) : ICommand<ApiResult<object>>;

public sealed class AddProductToCollectionValidator : AbstractValidator<AddProductToCollectionCommand>
{
    public AddProductToCollectionValidator()
    {
        RuleFor(x => x.CollectionId)
            .NotEmpty();
        RuleFor(x => x.ProductId)
            .NotEmpty();
    }
}

internal sealed class AddProductToCollectionCommandHandler(ICollectionRepository collections, IProductRepository products, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<AddProductToCollectionCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(AddProductToCollectionCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var collection = await collections.GetTrackedRequiredAsync(request.CollectionId, ct);
            var product = await products.GetRequiredByIdAsync(request.ProductId, ct);
            collection.AddProduct(product);
            return ApiResultBuilder.Success<object>(collection.Id);
        }, cancellationToken);
}