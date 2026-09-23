using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UltraSol.Modules.Payment.Infrastructure.Persistence;

public sealed class PaymentDesignTimeDbContextFactory : IDesignTimeDbContextFactory<PaymentDbContext>
{
    public PaymentDbContext CreateDbContext(string[] args)
    {
        var index = Array.IndexOf(args, "--connection");
        var connection = index >= 0 && index + 1 < args.Length ? args[index + 1] : "Host=localhost;Database=payment_design;Username=unused";
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseNpgsql(connection, postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", Schema.Name)).Options;
        return new PaymentDbContext(options);
    }
}
