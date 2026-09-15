using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Modules.Auth.Domain.Accounts;
using UltraSol.Modules.Auth.Domain.Sessions;
using UltraSol.Modules.Auth.Infrastructure.Persistence;
using UltraSol.Modules.Auth.Infrastructure.Repositories;
using UltraSol.Shared.Domain.Common.Repositories;
using UltraSol.Shared.Infrastructure.Repositories;
using UltraSol.Modules.Auth.Application.Authentication;
using UltraSol.Modules.Auth.Application.Persistence;
using UltraSol.Modules.Auth.Application.Authorization;
using UltraSol.Modules.Auth.Infrastructure.Authentication;
using UltraSol.Shared.Infrastructure.Persistence.PostgreSQL;

namespace UltraSol.Modules.Auth.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAuthRuntime(this IServiceCollection services)
    {
        services.AddScoped<IAuthTokens, AuthTokens>();
        services.AddScoped<AccountSessions>();
        services.AddScoped<AuthorizationRules>();
        services.AddScoped<IAuthRepository, AuthRepository>();
        return services;
    }

    public static IServiceCollection AddAuthInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPostgres<AuthDbContext>(Schema.Name);
        services.AddLogging();
        services.AddDataProtection();
        services.AddIdentityCore<UserAccount>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = false;
            options.SignIn.RequireConfirmedPhoneNumber = false;
            options.SignIn.RequireConfirmedAccount = false;
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version2;
            options.Stores.MaxLengthForKeys = 128;
        }).AddEntityFrameworkStores<AuthDbContext>().AddDefaultTokenProviders();
        services.TryAddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IAuthUnitOfWork, AuthUnitOfWork>();
        services.AddScoped<SessionRepository>();
        services.AddScoped<IRepository<Session>>(provider => provider.GetRequiredService<SessionRepository>());
        services.AddScoped<IRepository<Session, Guid>>(provider => provider.GetRequiredService<SessionRepository>());
        services.AddScoped<IQueryRepository<Session>>(provider => provider.GetRequiredService<SessionRepository>());
        services.AddScoped<ICommandRepository<Session>>(provider => provider.GetRequiredService<SessionRepository>());
        return services;
    }
}