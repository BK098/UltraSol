using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;

namespace UltraSol.Modules.Catalog.Infrastructure.Persistence.Seeding;

public sealed class CatalogSeeder(CatalogDbContext context, ICatalogUnitOfWork unitOfWork)
{
    public async Task<bool> SeedAsync(string path, CancellationToken cancellationToken = default)
    {
        var data = await CatalogSeedData.ReadAsync(path, cancellationToken);
        var aggregates = await data.BuildAsync(cancellationToken);
        var skus = aggregates.OfType<ProductItem>().Select(x => x.Sku).ToArray();
        if (skus.Length == 0)
        {
            throw new InvalidDataException("Catalog seed must contain product items.");
        }
        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var existing = await context.ProductItems.CountAsync(x => skus.Contains(x.Sku), ct);
            if (existing == skus.Length)
            {
                return false;
            }
            if (existing > 0)
            {
                throw new InvalidOperationException("Some seed SKUs already exist. Resolve the collision before seeding; existing data will not be overwritten.");
            }
            foreach (var aggregate in aggregates)
            {
                context.Add(aggregate);
            }
            return true;
        }, cancellationToken);
    }
}