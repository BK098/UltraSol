using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Products.Commands;

public sealed record ChangeProductBrandDto(Guid? BrandId);
public sealed record ChangeProductBrandCommand(Guid ProductId, ChangeProductBrandDto Model) : ICommand<ApiResult<object>>;

public sealed class ChangeProductBrandValidator : AbstractValidator<ChangeProductBrandCommand>
{
    public ChangeProductBrandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Model.BrandId!.Value).NotEmpty().When(x => x.Model.BrandId.HasValue);
    }
}

internal sealed class ChangeProductBrandCommandHandler(
    IProductRepository products,
    IBrandRepository brands,
    ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<ChangeProductBrandCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ChangeProductBrandCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var product = await products.GetTrackedRequiredAsync(request.ProductId, ct);
            if (request.Model.BrandId is null)
            {
                product.ClearBrand();
            }
            else
            {
                var brand = await brands.GetRequiredByIdAsync(request.Model.BrandId.Value, ct);
                product.AssignBrand(brand);
            }
            return ApiResultBuilder.Success<object>(product.Id);
        }, cancellationToken);
}
