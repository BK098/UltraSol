using UltraSol.Shared.Application.Authentication;
using MediatR;
using UltraSol.Shared.Application.Services;

namespace UltraSol.Modules.Auth.Application.Authorization;

public sealed class PermissionBehavior<TRequest, TResponse>(ICurrentAccount current, IPermissionService permissions) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is IAnonymousAuthRequest)
        {
            return await next(ct);
        }
        if (current.UserId is null)
        {
            throw new AuthAccessException(401, "Authentication required.");
        }
        if (request is ISystemAuthRequest)
        {
            if (!(await permissions.GetAsync(current.UserId.Value, ct)).IsSystem)
            {
                throw new AuthAccessException(403, "System access required.");
            }
        }
        else if (request is not ISelfServiceAuthRequest)
        {
            await permissions.DemandAsync(PermissionCatalog.GetPermission(typeof(TRequest)), ct);
        }
        return await next(ct);
    }
}



public interface ISystemAuthRequest;