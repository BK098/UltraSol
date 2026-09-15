//using Microsoft.EntityFrameworkCore;
//using Microsoft.EntityFrameworkCore.Design;
//using Microsoft.Extensions.Configuration;

//namespace UltraSol.Modules.Auth.Infrastructure.Persistence;

//public sealed class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
//{
//    public AuthDbContext CreateDbContext(string[] args)
//    {
//        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
//        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UltraSol.slnx")))
//        {
//            directory = directory.Parent;
//        }
//        if (directory is null)
//        {
//            throw new InvalidOperationException("Run Auth EF commands from the UltraSol repository.");
//        }
//        var settingsPath = Path.Combine(directory.FullName, "src", "Bootstrappers", "UltraSol.Bootstrappers");
//        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
//            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
//        var configurationBuilder = new ConfigurationBuilder()
//            .SetBasePath(settingsPath)
//            .AddJsonFile("appsettings.json")
//            .AddJsonFile($"appsettings.{environment}.json", optional: true);
//var configuration = configurationBuilder.AddEnvironmentVariables().AddCommandLine(args).Build();
//        return new AuthDbContext(AuthPostgres.Options(configuration));
//    }
//}

//internal static class AuthPostgres
//{
//    internal static DbContextOptions<AuthDbContext> Options(IConfiguration configuration)
//    {
//        var options = new DbContextOptionsBuilder<AuthDbContext>();
//        Configure(options, configuration);
//        return options.Options;
//    }

//    internal static void Configure(DbContextOptionsBuilder options, IConfiguration configuration)
//    {
//        var connection = configuration["Postgres:ConnectionString"];
//        if (string.IsNullOrWhiteSpace(connection))
//        {
//            throw new InvalidOperationException("Postgres:ConnectionString is required.");
//        }
//        options.UseNpgsql(connection, postgres =>
//        {
//            postgres.MigrationsAssembly(typeof(AuthDbContext).Assembly.FullName);
//            postgres.MigrationsHistoryTable("__EFMigrationsHistory", Schema.Name);
//        });
//    }
//}