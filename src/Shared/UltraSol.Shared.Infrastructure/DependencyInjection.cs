using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi;
using System.Runtime.CompilerServices;
using UltraSol.Shared.Application;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Repositories;
using UltraSol.Shared.Infrastructure.Api;
using UltraSol.Shared.Infrastructure.Exceptions;
using UltraSol.Shared.Infrastructure.Persistence.PostgreSQL;
using UltraSol.Shared.Infrastructure.Repositories;
using UltraSol.Shared.Infrastructure.ThirdParties.Caching.Redis;
using UltraSol.Shared.Infrastructure.ThirdParties.MessageQueues.RabbitMessageQueues;

[assembly: InternalsVisibleTo("UltraSol.Bootstrappers")]
namespace UltraSol.Shared.Infrastructure
{
    internal static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddProblemDetails();
            services.AddExceptionHandler<GlobalExceptionHandler>();
            services.AddControllers().ConfigureApplicationPartManager(manager =>
            {
                manager.FeatureProviders.Add(new InternalControllerFeatureProvider());
            });
            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(entry => entry.Value is { Errors.Count: > 0 })
                        .ToDictionary(
                            entry => entry.Key,
                            entry => entry.Value!.Errors
                                .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "The supplied value is invalid." : error.ErrorMessage)
                                .ToArray());

                    var response = ApiResultBuilder.Validation<object>(errors, traceId: context.HttpContext.TraceIdentifier);
                    return new BadRequestObjectResult(response);
                };
            });
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options => options.SwaggerDoc("v1", new OpenApiInfo { Title = "UltraSol API", Version = "v1" }));
            services.AddApplication();

            services.AddPostgres();
            services.AddRedis();
            services.AddRabbitMessageQueues();

            services.TryAddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
            services.AddCors(options =>
            {
                options.AddPolicy("Frontend", policy =>
                {
                    var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
                    policy
                        .WithOrigins(origins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });
            return services;
        }
        public static IApplicationBuilder UseInfrastructure(this IApplicationBuilder app)
        {
            app.UseExceptionHandler();
            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseCors("Frontend");
            return app;
        }

        internal static TOptions GetOptions<TOptions>(this IServiceCollection services, string sectionName)
            where TOptions : class, new()
        {
            using var serviceProvider = services.BuildServiceProvider();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var section = configuration.GetSection(sectionName);
            var options = new TOptions();
            section.Bind(options);
            return options;
        }
    }
}
