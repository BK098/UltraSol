//using Microsoft.EntityFrameworkCore;
//using Microsoft.EntityFrameworkCore.Design;

//namespace UltraSol.Modules.Inventory.Infrastructure.Persistence;

//public sealed class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
//{
//    public InventoryDbContext CreateDbContext(string[] args)
//    {
//        var connectionIndex = Array.IndexOf(args, "--connection");
//        var connection = connectionIndex >= 0 && connectionIndex + 1 < args.Length
//            ? args[connectionIndex + 1]
//            : "Host=localhost;Database=inventory_design;Username=unused";
//        var options = new DbContextOptionsBuilder<InventoryDbContext>()
//            .UseNpgsql(connection, postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", Schema.Name))
//            .Options;
//        return new InventoryDbContext(options);
//    }
//}
