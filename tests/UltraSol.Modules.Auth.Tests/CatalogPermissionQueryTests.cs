using UltraSol.Modules.Auth.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Catalog.Application.Features.Products.Queries;
using UltraSol.Modules.Catalog.Infrastructure.Reads;
using UltraSol.Modules.Catalog.Infrastructure.Persistence;
using UltraSol.Modules.Organization.Infrastructure.Persistence;
using UltraSol.Shared.Domain.Common.Paging;
using Xunit;

namespace UltraSol.Modules.Auth.Tests;

public sealed class CatalogPermissionQueryTests
{
    [Fact]
    public void OrganizationMapsOnlyItsOwnSchemaAndMailbox()
    {
        using var db = new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>().UseNpgsql("Host=localhost;Database=translation_only;Username=unused").Options);
        Assert.All(db.Model.GetEntityTypes(), type => Assert.Equal("organization", type.GetSchema()));
        var sql = db.Database.GenerateCreateScript();
        Assert.Contains("organization.outbox", sql);
        Assert.Contains("organization.inbox", sql);
        Assert.DoesNotContain("auth.", sql);
    }


}