using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerGen;
using UltraSol.Modules.Catalog.Application.Reads;

namespace UltraSol.Modules.Catalog.Infrastructure.Reads;

public static class CatalogReadRegistration
{
    public static IServiceCollection AddCatalogReads(this IServiceCollection services)
    {
        services.AddScoped<ICatalogReadStore, CatalogReadStore>();
        // Query-local nested DTO names must remain distinct in OpenAPI.
        services.Configure<SwaggerGenOptions>(options => options.CustomSchemaIds(SchemaId));
        return services;
    }

    private static string SchemaId(Type type)
    {
        if (type.IsArray)
        {
            return SchemaId(type.GetElementType()!) + "Array";
        }
        if (type.IsGenericType)
        {
            return type.Name.Split('`')[0] + "Of" + string.Join("And", type.GetGenericArguments().Select(SchemaId));
        }
        return type.DeclaringType is null ? type.Name : SchemaId(type.DeclaringType) + type.Name;
    }
}