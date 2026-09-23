using UltraSol.Shared.Application.Authentication;
using System.Net;
using System.Net.Http.Headers;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UltraSol.Modules.Auth.Application.Authorization;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Modules.Catalog.Infrastructure.Persistence;
using UltraSol.Modules.Catalog.Infrastructure.Repositories;
using UltraSol.Modules.Catalog.Infrastructure.Reads;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Application.Features.ProductItems.Queries;
using UltraSol.Modules.Pricing.Api;
using UltraSol.Shared.Domain.Common.Repositories;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Infrastructure.Api;
using UltraSol.Shared.Infrastructure.Exceptions;

namespace UltraSol.Modules.Pricing.Tests;

internal sealed class PricingHttpHost(WebApplication app, HttpClient client, PricingTestClock clock) : IAsyncDisposable
{
    public WebApplication App { get; } = app;
    public HttpClient Client { get; } = client;
    public PricingTestClock Clock { get; } = clock;
    public static readonly Guid Actor = Guid.NewGuid();

    public static async Task<PricingHttpHost> Start(string connection = "Host=localhost;Database=unused;Username=unused", Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Postgres:ConnectionString"] = connection });
        var clock = new PricingTestClock(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddPricingModule(builder.Configuration);
        builder.Services.AddSingleton<IDomainEventDispatcher, PricingTestDispatcher>();
        builder.Services.AddMediatR(options => options.RegisterServicesFromAssembly(typeof(SkuExistsQuery).Assembly));
        builder.Services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(connection));
        builder.Services.AddScoped<IProductItemRepository, ProductItemRepository>();
        builder.Services.AddCatalogReads();
        builder.Services.AddScoped<IClientCatalogReadStore, ClientCatalogReadStore>();
        builder.Services.AddScoped<ICurrentAccount, HttpTestAccount>();
        builder.Services.AddScoped<IPermissionService, HttpTestPermissions>();
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PermissionBehavior<,>));
        builder.Services.AddAuthentication("PricingTest").AddScheme<AuthenticationSchemeOptions, PricingTestAuthentication>("PricingTest", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        var providerType = typeof(BaseController).Assembly.GetType("UltraSol.Shared.Infrastructure.Api.InternalControllerFeatureProvider")!;
        var filterType = System.Reflection.Assembly.Load("UltraSol.Modules.Auth.Api").GetType("UltraSol.Modules.Auth.Api.AuthErrorsFilter")!;
        builder.Services.AddControllers(options => options.Filters.Add(filterType))
            .AddApplicationPart(typeof(PricingModule).Assembly)
            .AddApplicationPart(System.Reflection.Assembly.Load("UltraSol.Modules.Catalog.Api"))
            .ConfigureApplicationPartManager(manager => manager.FeatureProviders.Add((IApplicationFeatureProvider<ControllerFeature>)Activator.CreateInstance(providerType)!));
        builder.Services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState.Where(entry => entry.Value!.Errors.Count > 0)
                .ToDictionary(entry => entry.Key, entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray());
            return new BadRequestObjectResult(ApiResultBuilder.Validation<object>(errors));
        });
        builder.Services.AddSwaggerGen();
        configureServices?.Invoke(builder.Services);
        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        builder.Configuration["Pricing:CatalogApi:BaseUrl"] = app.Urls.Single();
        var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()), Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Actor.ToString());
        return new PricingHttpHost(app, client, clock);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await App.StopAsync();
        await App.DisposeAsync();
    }
}

internal sealed class PricingTestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class HttpTestAccount(IHttpContextAccessor accessor) : ICurrentAccount
{
    public Guid? UserId => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var value) ? value : null;
    public Guid? SessionId => null;
}

internal sealed class HttpTestPermissions(IHttpContextAccessor accessor) : IPermissionService
{
    public Task<PermissionSnapshot> GetAsync(Guid userId, CancellationToken ct) => Task.FromResult(new PermissionSnapshot(false, [], []));
    public Task DemandAsync(string permission, CancellationToken ct)
    {
        if (accessor.HttpContext!.Request.Headers["X-Test-Deny"].Contains(permission))
        {
            throw new AuthAccessException(403, "Permission denied.");
        }
        return Task.CompletedTask;
    }
}

internal sealed class PricingTestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.Ordinal) || !Guid.TryParse(authorization[7..], out var id))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        return Response.WriteAsJsonAsync(ApiResultBuilder.Unauthorized<object>());
    }
}