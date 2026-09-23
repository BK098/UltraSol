using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UltraSol.Modules.Auth.Application.Authorization;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Modules.Ordering.Api;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Shared.Application.Authentication;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Repositories;
using UltraSol.Shared.Infrastructure.Api;
using UltraSol.Shared.Infrastructure.Exceptions;

namespace UltraSol.Modules.Ordering.Tests;

internal sealed class OrderingHttpHost(WebApplication app, HttpClient client) : IAsyncDisposable
{
    public WebApplication App { get; } = app;
    public HttpClient Client { get; } = client;

    public static async Task<OrderingHttpHost> Start(string connection, OrderingTestClock clock, CheckoutDependencies? dependencies,
        Action<WebApplicationBuilder>? configure = null, Action<WebApplication>? routes = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Postgres:ConnectionString"] = connection });
        builder.Services.AddSingleton<TimeProvider>(clock);
        configure?.Invoke(builder);
        builder.Services.AddOrderingModule(builder.Configuration);
        builder.Services.AddSingleton<IDomainEventDispatcher, OrderingTestDispatcher>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentAccount, OrderingHttpAccount>();
        builder.Services.AddScoped<IPermissionService, OrderingHttpPermissions>();
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PermissionBehavior<,>));
        if (dependencies is not null)
        {
            builder.Services.AddSingleton<ICatalogCheckoutClient>(dependencies);
            builder.Services.AddSingleton<IPricingQuoteClient>(dependencies);
            builder.Services.AddSingleton<IInventoryReservationClient>(dependencies);
        }
        // Drive recovery explicitly so each fault/race test controls its exact boundary.
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.AddAuthentication("OrderingTest").AddScheme<AuthenticationSchemeOptions, OrderingTestAuthentication>("OrderingTest", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        var internalControllers = typeof(BaseController).Assembly.GetType("UltraSol.Shared.Infrastructure.Api.InternalControllerFeatureProvider")!;
        var errors = System.Reflection.Assembly.Load("UltraSol.Modules.Auth.Api").GetType("UltraSol.Modules.Auth.Api.AuthErrorsFilter")!;
        builder.Services.AddControllers(options => options.Filters.Add(errors)).AddApplicationPart(typeof(OrderingModule).Assembly)
            .ConfigureApplicationPartManager(manager => manager.FeatureProviders.Add((IApplicationFeatureProvider<ControllerFeature>)Activator.CreateInstance(internalControllers)!));
        builder.Services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
            new BadRequestObjectResult(ApiResultBuilder.Validation<object>(context.ModelState.Where(entry => entry.Value!.Errors.Count > 0)
                .ToDictionary(entry => entry.Key, entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray()))));
        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        routes?.Invoke(app);
        await app.StartAsync();
        foreach (var module in new[] { "Auth", "Catalog", "Pricing", "Inventory" })
        {
            builder.Configuration[$"Ordering:{module}Api:BaseUrl"] = app.Urls.Single();
        }
        return new(app, new HttpClient { BaseAddress = new Uri(app.Urls.Single()), Timeout = TimeSpan.FromSeconds(30) });
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await App.StopAsync();
        await App.DisposeAsync();
    }
}

internal sealed class OrderingHttpAccount(IHttpContextAccessor accessor) : ICurrentAccount
{
    public Guid? UserId => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var value) ? value : null;
    public Guid? SessionId => null;
}

internal sealed class OrderingHttpPermissions(IHttpContextAccessor accessor) : IPermissionService
{
    public Task<PermissionSnapshot> GetAsync(Guid userId, CancellationToken ct) => Task.FromResult(new PermissionSnapshot(false, [], []));
    public Task DemandAsync(string permission, CancellationToken ct)
    {
        if (accessor.HttpContext!.Request.Headers["X-Test-Admin"] != "true" && accessor.HttpContext.User.FindFirstValue("test-service") != "true")
        {
            throw new AuthAccessException(403, "Permission denied.");
        }
        return Task.CompletedTask;
    }
}

internal sealed class OrderingTestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }
        var value = authorization[7..];
        var service = value.StartsWith("test.", StringComparison.Ordinal);
        if (!Guid.TryParse(value, out var id) && !service)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, (service ? ServiceActor : id).ToString()),
            new Claim("test-service", service ? "true" : "false")], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
    internal static Guid ServiceActor { get; } = Guid.NewGuid();
}