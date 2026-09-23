using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Modules.Auth.Domain.Accounts;
using UltraSol.Modules.Auth.Domain.Sessions;
using UltraSol.Modules.Auth.Infrastructure;
using UltraSol.Modules.Auth.Infrastructure.Persistence;
using UltraSol.Modules.Auth.Infrastructure.Repositories;
using UltraSol.Shared.Domain.Common.Repositories;
using Xunit;

namespace UltraSol.Modules.Auth.Tests;

public sealed class AuthModelTests
{
    internal static ServiceProvider Services(string connection, IDomainEventDispatcher? dispatcher = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Postgres:ConnectionString"] = connection
        }).Build();
        var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration).AddAuthInfrastructure(configuration);
        if (dispatcher is not null)
        {
            services.AddScoped(_ => dispatcher);
        }
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    [Fact]
    public void ModelAndServicesUseOneContextWithoutRoles()
    {
        using var services = Services("Host=localhost;Database=auth_model_only;Username=unused");
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var context = provider.GetRequiredService<AuthDbContext>();
        var user = context.Model.FindEntityType(typeof(UserAccount))!;
        Assert.All(context.Model.GetEntityTypes(), entity => Assert.Equal("auth", entity.GetSchema()));
        Assert.DoesNotContain(context.Model.GetEntityTypes(), entity => entity.ClrType == typeof(IdentityRole<Guid>) || entity.ClrType == typeof(IdentityUserRole<Guid>) || entity.ClrType.Name.Contains("Passkey"));
        Assert.NotEmpty(user.GetDeclaredQueryFilters());
        Assert.True(user.FindProperty(nameof(UserAccount.ConcurrencyStamp))!.IsConcurrencyToken);
        Assert.All(user.GetIndexes().Where(index => index.Properties[0].Name.StartsWith("Normalized")), index =>
        {
            Assert.True(index.IsUnique);
            Assert.Equal("\"Status\" <> 2", index.GetFilter());
        });
        Assert.Same(provider.GetRequiredService<SessionRepository>(), provider.GetRequiredService<IRepository<Session>>());
        var store = Assert.IsAssignableFrom<UserOnlyStore<UserAccount, AuthDbContext, Guid, IdentityUserClaim<Guid>, IdentityUserLogin<Guid>, IdentityUserToken<Guid>>>(provider.GetRequiredService<IUserStore<UserAccount>>());
        Assert.Same(context, store.Context);
        Assert.True(store.AutoSaveChanges);
        Assert.NotNull(provider.GetRequiredService<UserManager<UserAccount>>());
        Assert.NotNull(provider.GetRequiredService<IAuthUnitOfWork>());
        var options = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;
        Assert.True(options.User.RequireUniqueEmail);
        Assert.False(options.SignIn.RequireConfirmedEmail);
        Assert.False(options.SignIn.RequireConfirmedPhoneNumber);
        Assert.False(options.SignIn.RequireConfirmedAccount);
        using var designContext = new AuthDbContext(new DbContextOptionsBuilder<AuthDbContext>()
            .UseNpgsql("Host=localhost;Database=auth_model_only;Username=unused").Options);
        Assert.Equal(context.Database.GenerateCreateScript(), designContext.Database.GenerateCreateScript());
    }

    [Fact]
    public async Task WritesRequireAuthUnitOfWorkAndSynchronousSavesAreRejected()
    {
        using var services = Services("Host=localhost;Database=auth_model_only;Username=unused");
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        Assert.Throws<NotSupportedException>(() => context.SaveChanges());
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<UserAccount>>();
        Assert.False((await manager.PasswordValidators[0].ValidateAsync(manager, UserAccount.Create("a@example.com", DateTimeOffset.UtcNow), "x")).Succeeded);
    }
}