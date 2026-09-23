using UltraSol.Shared.Application.Authentication;
using MediatR;
using UltraSol.Modules.Auth.Application.Authorization;
using UltraSol.Modules.Auth.Application.Features.Accounts.Commands;
using UltraSol.Modules.Auth.Application.Features.Accounts.Queries;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Modules.Catalog.Application.Features.Products.Queries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;
using Xunit;

namespace UltraSol.Modules.Auth.Tests;

public sealed class PermissionBehaviorTests
{
    [Fact]
    public async Task Login_and_registration_allow_anonymous_without_demanding_feature_permission()
    {
        var permissions = new Permissions(false);
        var login = new PermissionBehavior<LoginCommand, ApiResult<object>>(new Account(false), permissions);
        var result = await login.Handle(new LoginCommand("buyer@example.test", "test-password", "token"),
            _ => Task.FromResult(ApiResultBuilder.Success<object>("reached credentials check")), default);
        Assert.True(result.IsSuccess);
        Assert.Null(permissions.LastPermission);
        Assert.IsAssignableFrom<IAnonymousAuthRequest>(new RegisterCommand("buyer@example.test", "test-password"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Self_service_requires_identity_but_no_feature_grant(bool authenticated)
    {
        var permissions = new Permissions(false);
        var behavior = new PermissionBehavior<MeQuery, ApiResult<object>>(new Account(authenticated), permissions);
        var called = false;
        Task<ApiResult<object>> Next(CancellationToken ct)
        {
            called = true;
            return Task.FromResult(ApiResultBuilder.Success<object>("self"));
        }
        if (authenticated)
        {
            await behavior.Handle(new MeQuery(), Next, default);
            Assert.True(called);
        }
        else
        {
            Assert.Equal(401, (await Assert.ThrowsAsync<AuthAccessException>(() => behavior.Handle(new MeQuery(), Next, default))).StatusCode);
            Assert.False(called);
        }
        Assert.Null(permissions.LastPermission);
    }

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