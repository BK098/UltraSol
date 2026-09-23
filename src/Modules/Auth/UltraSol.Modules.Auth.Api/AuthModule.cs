using UltraSol.Shared.Application.Authentication;
using MediatR;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using UltraSol.Modules.Auth.Application.Authorization;
using UltraSol.Modules.Auth.Application.Messaging;
using UltraSol.Modules.Auth.Application.Persistence;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Modules.Auth.Infrastructure;
using UltraSol.Modules.Auth.Infrastructure.Authentication;
using UltraSol.Modules.Auth.Infrastructure.Authorization;
using UltraSol.Modules.Auth.Infrastructure.Messaging;
using UltraSol.Modules.Auth.Infrastructure.Persistence;
using UltraSol.Shared.Application.Messaging.Integration;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Infrastructure.DependencyInjections;
using UltraSol.Shared.Infrastructure.Messaging;
using UltraSol.Shared.Infrastructure.Persistence.PostgreSQL;
using UltraSol.Shared.IntegrationEvents.Organization;

[assembly: InternalsVisibleTo("UltraSol.Bootstrappers")]
namespace UltraSol.Modules.Auth.Api;

internal static class AuthModule
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthInfrastructure(configuration);
        services.AddAuthRuntime();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentAccount, CurrentAccount>();
        services.AddScoped<PermissionService>();
        services.AddScoped<IPermissionService>(sp => sp.GetRequiredService<PermissionService>());
        services.AddScoped<PermissionCatalogSynchronizer>();
        services.AddHostedService<PermissionCacheWorker>();
        services.AddScoped<Mailbox<AuthDbContext>>(sp => new(sp.GetRequiredService<AuthDbContext>(), sp.GetRequiredService<IAuthUnitOfWork>(), sp));
        services.AddScoped<IAuthOutbox, AuthOutbox>();
        services.AddScoped<IIntegrationHandler<EmployeeAccountRequested>, EmployeeAccountRequestedHandler>();
        services.AddScoped<IIntegrationHandler<EmployeeAccessChanged>, EmployeeAccessChangedHandler>();
        services.AddSingleton(new ModuleMailbox("auth", sp => sp.GetRequiredService<Mailbox<AuthDbContext>>(), [typeof(EmployeeAccountRequested), typeof(EmployeeAccessChanged)]));

        services.AddScoped<SessionAuthentication>();
        services.AddScoped<AuthSeeder>();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PermissionBehavior<,>));
        services.AddRegistration(AppDomain.CurrentDomain.GetAssemblies());
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        var auth = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
        if (Encoding.UTF8.GetByteCount(auth.SigningKey) < 32 || auth.AccessTokenMinutes <= 0 || auth.SessionDays <= 0)
        {
            throw new InvalidOperationException("Auth:SigningKey must contain at least 32 bytes and token/session lifetimes must be positive.");
        }
        services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
        services.AddControllers(options =>
        {
            options.Filters.Add<AuthErrorsFilter>();
            options.Filters.Add<AuthCsrfFilter>();
        }).AddApplicationPart(typeof(AuthModule).Assembly);
        services.AddAuthentication(options =>
        {
            options.DefaultScheme = "UltraSol";
            options.DefaultChallengeScheme = "UltraSol";
        })
            .AddPolicyScheme("UltraSol", null, options => options.ForwardDefaultSelector = context =>
                context.Request.Headers.Authorization.Count > 0 ? JwtBearerDefaults.AuthenticationScheme : "AuthCookie")
            .AddCookie("AuthCookie", options =>
            {
                options.Cookie.Name = ".UltraSol.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.SlidingExpiration = false;
                options.Events.OnValidatePrincipal = async context =>
                {
                    if (!await context.HttpContext.RequestServices.GetRequiredService<SessionAuthentication>().ValidateAsync(context.Principal, context.HttpContext.RequestAborted))
                    {
                        context.RejectPrincipal();
                    }
                };
                options.Events.OnRedirectToLogin = context => context.Response.WriteAsJsonAsync(ApiResultBuilder.Unauthorized<object>(), Status(context.HttpContext, 401));
                options.Events.OnRedirectToAccessDenied = context => context.Response.WriteAsJsonAsync(ApiResultBuilder.Forbidden<object>(), Status(context.HttpContext, 403));
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = auth.Issuer, ValidateAudience = true, ValidAudience = auth.Audience,
                    ValidateLifetime = true, ValidateIssuerSigningKey = true, RequireSignedTokens = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.SigningKey)), ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.Zero
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        if (!await context.HttpContext.RequestServices.GetRequiredService<SessionAuthentication>().ValidateAsync(context.Principal, context.HttpContext.RequestAborted))
                        {
                            context.Fail("Session is invalid.");
                        }
                    }
                };
            });
        services.AddAuthorization();
        return services;
    }

    public static async Task InitializeAuthAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<AuthSeeder>().SeedAsync(Path.Combine(AppContext.BaseDirectory, "seeds", "auth.json"), app.Lifetime.ApplicationStopping);
    }

    public static async Task<IApplicationBuilder> UseAuthModule(this IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        await app.ApplicationServices.MigrateAsync<AuthDbContext>();
        return app;
    }

    private static CancellationToken Status(HttpContext context, int status)
    {
        context.Response.StatusCode = status;
        return context.RequestAborted;
    }
}

internal sealed class AuthErrorsFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is AuthAccessException error)
        {
            var response = error.StatusCode == 401 ? ApiResultBuilder.Unauthorized<object>(error.Message) : ApiResultBuilder.Forbidden<object>(error.Message);
            context.Result = new ObjectResult(response) { StatusCode = error.StatusCode };
            context.ExceptionHandled = true;
        }
    }
}

internal sealed class AuthCsrfFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method))
        {
            return;
        }
        if (request.Path == "/api/auth/login" || request.Headers.Authorization.Count == 0 && request.Cookies.ContainsKey(".UltraSol.Auth"))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context.HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                context.Result = new ObjectResult(ApiResultBuilder.Forbidden<object>("Invalid CSRF token")) { StatusCode = 403 };
            }
        }
    }
}