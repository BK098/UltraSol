using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using UltraSol.Modules.Organization.Application.Messaging;
using UltraSol.Modules.Organization.Domain.Repositories;
using UltraSol.Modules.Organization.Infrastructure.Messaging;
using UltraSol.Modules.Organization.Infrastructure.Persistence;
using UltraSol.Modules.Organization.Infrastructure.Repositories;
using UltraSol.Shared.Application.Messaging.Integration;
using UltraSol.Shared.Infrastructure.DependencyInjections;
using UltraSol.Shared.Infrastructure.Messaging;
using UltraSol.Shared.Infrastructure.Persistence.PostgreSQL;
using UltraSol.Shared.IntegrationEvents.Auth;
[assembly: InternalsVisibleTo("UltraSol.Bootstrappers")]
namespace UltraSol.Modules.Organization.Api;
internal static class OrganizationModule
{
    public static IServiceCollection AddOrganizationModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPostgres<OrganizationDbContext>(Schema.Name);
        services.AddRegistration(AppDomain.CurrentDomain.GetAssemblies());
        services.AddScoped<IOrganizationUnitOfWork, OrganizationUnitOfWork>();
        services.AddScoped<Mailbox<OrganizationDbContext>>(sp => new(sp.GetRequiredService<OrganizationDbContext>(), sp.GetRequiredService<IOrganizationUnitOfWork>(), sp));
        services.AddScoped<IOrganizationOutbox, OrganizationOutbox>();
        services.AddScoped<IIntegrationHandler<EmployeeAccountProvisioned>, EmployeeAccountProvisionedHandler>();
        services.AddScoped<IIntegrationHandler<EmployeeAccessApplied>, EmployeeAccessAppliedHandler>();
        services.AddSingleton(new ModuleMailbox("organization", sp => sp.GetRequiredService<Mailbox<OrganizationDbContext>>(), [typeof(EmployeeAccountProvisioned), typeof(EmployeeAccessApplied)]));
        return services;
    }
    public static async Task<IApplicationBuilder> UseOrganizationModule(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        await app.ApplicationServices.MigrateAsync<OrganizationDbContext>();

        return app;
    }
    //public static async Task InitializeOrganizationAsync(this WebApplication app)
    //{
    //    await using var scope = app.Services.CreateAsyncScope();
    //    await scope.ServiceProvider.GetRequiredService<OrganizationDbContext>().Database.MigrateAsync();
    //}
}