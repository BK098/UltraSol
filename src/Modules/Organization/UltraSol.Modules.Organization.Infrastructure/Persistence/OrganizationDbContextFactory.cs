using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace UltraSol.Modules.Organization.Infrastructure.Persistence;

//public sealed class OrganizationDbContextFactory : IDesignTimeDbContextFactory<OrganizationDbContext>
//{
//    public OrganizationDbContext CreateDbContext(string[] args)
//    {
//        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
//        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UltraSol.slnx")))
//        {
//            directory = directory.Parent;
//        }
//        if (directory is null)
//        {
//            throw new InvalidOperationException("Run Organization EF commands from the UltraSol repository.");
//        }
//        var settingsPath = Path.Combine(directory.FullName, "src", "Bootstrappers", "UltraSol.Bootstrappers");
//        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
//            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
//        var configurationBuilder = new ConfigurationBuilder()
//            .SetBasePath(settingsPath)
//            .AddJsonFile("appsettings.json")
//            .AddJsonFile($"appsettings.{environment}.json", optional: true);
//var configuration = configurationBuilder.AddEnvironmentVariables().AddCommandLine(args).Build();
//        return new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>().UseNpgsql(configuration["Postgres:ConnectionString"], postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", "organization")).Options);
//    }
//}