using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;
using System.Runtime.CompilerServices;
using UltraSol.Shared.Application;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Repositories;
using UltraSol.Shared.Infrastructure.Api;
using UltraSol.Shared.Infrastructure.Exceptions;
using UltraSol.Shared.Infrastructure.Persistence.PostgreSQL;
using UltraSol.Shared.Infrastructure.Repositories;
using UltraSol.Shared.Infrastructure.ThirdParties.Caching.Redis;

[assembly: InternalsVisibleTo("UltraSol.Bootstrappers")]
namespace UltraSol.Shared.Infrastructure
{
    internal static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, Assembly[] assemblies)
        {
            services.AddRedis(configuration);
            services.AddPostgres();
            services.AddApplication();
            services.AddControllers()
                .ConfigureApplicationPartManager(manager =>
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
            services.AddExceptionHandler<GlobalExceptionHandler>();

            services.TryAddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

            return services;
        }
        public static IApplicationBuilder UseInfrastructure(this IApplicationBuilder app)
        {
            return app;
        }
       
        public static T GetOptions<T>(this IServiceCollection services, string sectionName) where T : new()
        {
            using var serviceProvider = services.BuildServiceProvider();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var section = configuration.GetSection(sectionName);
            var options = new T();
            section.Bind(options);

            return options;
        }
    }
}