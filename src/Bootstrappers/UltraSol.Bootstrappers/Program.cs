using UltraSol.Shared.Infrastructure;
using UltraSol.Modules.Catalog.Api;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddInfrastructure(builder.Configuration, AppDomain.CurrentDomain.GetAssemblies());
builder.Services.AddCatalogModule(builder.Configuration);

var app = builder.Build();

//dotnet run --project src/Bootstrappers/UltraSol.Bootstrappers -- --Catalog:Seed=true
if (builder.Configuration.GetValue<bool>("Catalog:Seed"))
{
    await app.SeedCatalogAsync();
    return;
}

app.UseInfrastructure();
app.UseCatalogModule();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();