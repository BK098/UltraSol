using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Products.Commands;

public sealed record CreateProductDto(string? Name, string? Description);
public sealed record CreateProductCommand(CreateProductDto? Model) : ICommand<ApiResult<object>>;

public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Model).NotNull();
        RuleFor(x => x.Model!.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required.")
            .When(x => x.Model is not null);
    }
}

internal sealed class CreateProductCommandHandler(IProductRepository products, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<CreateProductCommand, ApiResult<object>>
{
    public async Task<ApiResult<object>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var product = Product.Create(request.Model!.Name!, request.Model.Description);
            await products.AddAsync(product, ct);
            return ApiResultBuilder.Success<object>(product.Id, statusCode: 201);
        }, cancellationToken);
    }
}