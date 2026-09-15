using UltraSol.Modules.Auth.Api;
using UltraSol.Modules.Catalog.Api;
using UltraSol.Modules.Organization.Api;
using UltraSol.Shared.Infrastructure;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuthModule(builder.Configuration);
builder.Services.AddCatalogModule(builder.Configuration);
builder.Services.AddOrganizationModule(builder.Configuration);

var app = builder.Build();
//await app.InitializeAuthAsync();
//await app.InitializeOrganizationAsync();
//await app.InitializeCatalogAsync();

//dotnet run --project src/Bootstrappers/UltraSol.Bootstrappers -- --Catalog:Seed=true
//if (builder.Configuration.GetValue<bool>("Catalog:Seed"))
//{
//    await app.SeedCatalogAsync();
//    return;
//}

app.UseOrganizationModule();
app.UseCatalogModule();
app.UseInfrastructure();

app.UseAuthModule();
app.UseHttpsRedirection();
app.MapControllers();
app.Run();