using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using UltraSol.Modules.Auth.Application.Features.Accounts.Commands;
using UltraSol.Modules.Catalog.Application.Features.Products.Commands;
using UltraSol.Modules.Inventory.Application.Features.Reservations.Commands;
using UltraSol.Modules.Organization.Application.Features.Employees.Commands;
using UltraSol.Modules.Pricing.Application.Features.Prices.Services;
using UltraSol.Modules.Pricing.Application.Features.Prices.Models;
using UltraSol.Modules.Pricing.Application.Features.NegotiatedPrices.Commands;
using UltraSol.Modules.Pricing.Infrastructure.Persistence;
using UltraSol.Shared.Application.Services;
using Xunit;

namespace UltraSol.Modules.Pricing.Tests;

public sealed class PricingArchitectureTests
{
    [Fact]
    public void Pricing_has_no_direct_reference_to_another_module()
    {
        foreach (var assembly in new[] { typeof(PriceResolver).Assembly, typeof(PricingDbContext).Assembly,
            typeof(UltraSol.Modules.Pricing.Domain.Prices.SkuPrice).Assembly })
        {
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference => reference.Name!.StartsWith("UltraSol.Modules.", StringComparison.Ordinal)
                && !reference.Name.StartsWith("UltraSol.Modules.Pricing.", StringComparison.Ordinal));
            Assert.DoesNotContain(assembly.GetTypes(), type => type.Name == "PricingActor");
        }
    }

    [Fact]
    public void Every_request_has_a_validator_that_rejects_null_models_and_empty_identifiers()
    {
        using var services = new ServiceCollection().AddSingleton(TimeProvider.System).BuildServiceProvider();
        var catalog = new PermissionCatalog([typeof(PriceResolver).Assembly]);
        foreach (var requestType in catalog.Requests.Keys)
        {
            var validatorContract = typeof(IValidator<>).MakeGenericType(requestType);
            var type = Assert.Single(typeof(PriceResolver).Assembly.GetTypes(), candidate => !candidate.IsAbstract && validatorContract.IsAssignableFrom(candidate));
            var validator = (IValidator)ActivatorUtilities.CreateInstance(services, type);
            var constructor = Assert.Single(requestType.GetConstructors());
            var parameters = constructor.GetParameters().Select(parameter => parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null).ToArray();
            var request = constructor.Invoke(parameters);
            var result = validator.Validate(new ValidationContext<object>(request));
            Assert.False(result.IsValid, requestType.Name);
        }
    }

    [Fact]
    public void Permission_catalog_preserves_all_modules_and_separate_negotiation_operations()
    {
        var previous = new PermissionCatalog([typeof(LoginCommand).Assembly, typeof(CreateProductCommand).Assembly,
            typeof(CreateEmployeeCommand).Assembly, typeof(ReserveStockCommand).Assembly]);
        var combined = new PermissionCatalog([typeof(LoginCommand).Assembly, typeof(CreateProductCommand).Assembly,
            typeof(CreateEmployeeCommand).Assembly, typeof(ReserveStockCommand).Assembly, typeof(PriceResolver).Assembly]);
        foreach (var pair in previous.Requests)
        {
            Assert.Equal(pair.Value, combined.Requests[pair.Key]);
        }
        var commands = new[] { typeof(CreateNegotiatedPriceCommand), typeof(SubmitNegotiatedPriceCommand), typeof(ApproveNegotiatedPriceCommand),
            typeof(RejectNegotiatedPriceCommand), typeof(RevokeNegotiatedPriceCommand) };
        Assert.Equal(5, commands.Select(type => combined.Requests[type]).Distinct().Count());
        Assert.Equal(37, combined.Requests.Keys.Count(type => type.Assembly == typeof(PriceResolver).Assembly));
    }

    [Fact]
    public void Migration_matches_model_and_has_deferred_overlap_and_immutable_audit_guards()
    {
        using var db = new PricingDbContext(new DbContextOptionsBuilder<PricingDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused", options => options.MigrationsHistoryTable("__EFMigrationsHistory", "pricing")).Options);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Contains(db.Database.GetMigrations(), migration => migration.EndsWith("InitialPricing", StringComparison.Ordinal));
        var sql = db.GetService<IMigrator>().GenerateScript();
        Assert.Contains("DEFERRABLE INITIALLY DEFERRED", sql);
        Assert.Contains("btree_gist", sql);
        Assert.Contains("tstzrange(effective_from, effective_to, '[)')", sql);
        Assert.Contains("protect_price_audit", sql);
        Assert.DoesNotContain("CREATE TABLE catalog.", sql);
        Assert.DoesNotContain("CREATE TABLE auth.", sql);
    }
}