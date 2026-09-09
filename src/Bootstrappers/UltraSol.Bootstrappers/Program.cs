using UltraSol.Shared.Infrastructure;
using UltraSol.Modules.Catalog.Api;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddOpenApi();
builder.Services.AddCatalogModule(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration, AppDomain.CurrentDomain.GetAssemblies());

var app = builder.Build();
app.UseCatalogModule();

app.MapOpenApi();
app.UseInfrastructure();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();