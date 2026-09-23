using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UltraSol.Modules.Ordering.Infrastructure.Persistence;

public sealed class OrderingDesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrderingDbContext>
{
    public OrderingDbContext CreateDbContext(string[] args)
    {
        var connectionIndex = Array.IndexOf(args, "--connection");
        var connection = connectionIndex >= 0 && connectionIndex + 1 < args.Length
            ? args[connectionIndex + 1]
            : "Host=localhost;Database=ordering_design;Username=unused";
        var options = new DbContextOptionsBuilder<OrderingDbContext>()
            .UseNpgsql(connection, postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", Schema.Name))
            .Options;
        return new OrderingDbContext(options);
    }
}