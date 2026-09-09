using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Products.Commands;

public sealed record CreateProductDto(string? Name, string? Description);
public sealed record CreateProductCommand(CreateProductDto? Model) : ICommand<ApiResult<ProductResponse>>;

public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Model!.Name).NotNull().WithName("Name");
    }
}

internal sealed class CreateProductCommandHandler(IProductRepository products, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<CreateProductCommand, ApiResult<ProductResponse>>
{
    public async Task<ApiResult<ProductResponse>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var product = Product.Create(request.Model!.Name!, request.Model.Description);
            await products.AddAsync(product, ct);
            return ApiResultBuilder.Success(ProductResponse.From(product), statusCode: 201);
        }, cancellationToken);
    }
}
