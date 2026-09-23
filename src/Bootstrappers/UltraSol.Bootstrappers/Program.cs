using UltraSol.Modules.Auth.Api;
using UltraSol.Modules.Catalog.Api;
using UltraSol.Modules.Inventory.Api;
using UltraSol.Modules.Ordering.Api;
using UltraSol.Modules.Payment.Api;
using UltraSol.Modules.Organization.Api;
using UltraSol.Modules.Pricing.Api;
using UltraSol.Shared.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthModule(builder.Configuration);
builder.Services.AddCatalogModule(builder.Configuration);
builder.Services.AddOrganizationModule(builder.Configuration);
builder.Services.AddInventoryModule(builder.Configuration);
builder.Services.AddPricingModule(builder.Configuration);
builder.Services.AddOrderingModule(builder.Configuration);
builder.Services.AddPaymentModule(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseInfrastructure();

await app.UseInventoryModule();
await app.UseOrganizationModule();
await app.UseCatalogModule();
await app.UseAuthModule();
await app.UsePricingModule();
await app.UseOrderingModule();
await app.UsePaymentModule();

app.MapControllers();
await app.RunAsync();
