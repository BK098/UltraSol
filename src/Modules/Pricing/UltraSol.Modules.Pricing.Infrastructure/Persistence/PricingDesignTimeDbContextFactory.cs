//using Microsoft.EntityFrameworkCore;
//using Microsoft.EntityFrameworkCore.Design;

//namespace UltraSol.Modules.Pricing.Infrastructure.Persistence;

//public sealed class PricingDesignTimeDbContextFactory : IDesignTimeDbContextFactory<PricingDbContext>
//{
//    public PricingDbContext CreateDbContext(string[] args)
//    {
//        // Migration generation is offline. Applying migrations requires an explicit connection.
//        var options = new DbContextOptionsBuilder<PricingDbContext>().UseNpgsql("Host=localhost;Database=pricing_design;Username=postgres", postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", Schema.Name)).Options;
//        return new PricingDbContext(options);
//    }
//}