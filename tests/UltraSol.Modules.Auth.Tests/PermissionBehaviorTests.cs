using MediatR;
using UltraSol.Modules.Auth.Application.Authorization;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Modules.Catalog.Application.Features.Products.Queries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;
using Xunit;

namespace UltraSol.Modules.Auth.Tests;

public sealed class PermissionBehaviorTests
{
    [Theory]
    [InlineData(false, false, 401)]
    [InlineData(true, false, 403)]
    [InlineData(true, true, 0)]
    public async Task PipelineChecksCatalogRequestBeforeCallingHandler(bool authenticated, bool allowed, int expectedStatus)
    {
        var permissions = new Permissions(allowed);
        var behavior = new PermissionBehavior<GetProductsQuery, ApiResult<object>>(new Account(authenticated), permissions);
        var called = false;
        Task<ApiResult<object>> Next(CancellationToken ct)
        {
            called = true;
            return Task.FromResult(ApiResultBuilder.Success<object>(new object()));
        }
        if (expectedStatus == 0)
        {
            await behavior.Handle(new GetProductsQuery(new PagedFilter()), Next, default);
            Assert.True(called);
        }
        else
        {
            var error = await Assert.ThrowsAsync<AuthAccessException>(() => behavior.Handle(new GetProductsQuery(new PagedFilter()), Next, default));
            Assert.Equal(expectedStatus, error.StatusCode);
            Assert.False(called);
        }
        Assert.Equal(authenticated ? "Catalog.Products.GetProducts" : null, permissions.LastPermission);
    }

    [Fact]
    public void ExplicitDenyWinsAndUnknownPermissionIsDenied()
    {
        var snapshot = new PermissionSnapshot(false, [], [new("read", false), new("read", true)]);
        Assert.False(snapshot.Allows("read"));
        Assert.False(snapshot.Allows("write"));
        Assert.True(new PermissionSnapshot(false, [], [new("read", false)]).Allows("read"));
        Assert.True(new PermissionSnapshot(true, ["System"], []).Allows("write"));
    }

    private sealed class Account(bool authenticated) : ICurrentAccount
    {
        public Guid? UserId => authenticated ? Guid.Parse("00000000-0000-0000-0000-000000000001") : null;
        public Guid? SessionId => null;
    }

    private sealed class Permissions(bool allowed) : IPermissionService
    {
        public string? LastPermission { get; private set; }
        public Task<PermissionSnapshot> GetAsync(Guid userId, CancellationToken ct) => Task.FromResult(new PermissionSnapshot(false, [], []));
        public Task DemandAsync(string permission, CancellationToken ct)
        {
            LastPermission = permission;
            if (!allowed)
            {
                throw new AuthAccessException(403, "Permission denied.");
            }
            return Task.CompletedTask;
        }
    }
}