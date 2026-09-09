using UltraSol.Modules.Catalog.Domain.Catalog.Products;

namespace UltraSol.Modules.Catalog.Application.Features.Products.Commands;

public sealed record ProductResponse(Guid Id, string Name, string? Description, ProductStatus Status)
{
    public static ProductResponse From(Product product) =>
        new(product.Id, product.Name, product.Description, product.Status);
}
