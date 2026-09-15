using UltraSol.Modules.Organization.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
namespace UltraSol.Modules.Organization.Application.Features.Employees.Queries;

public sealed record EmployeesQuery : IQuery<ApiResult<object>>;
internal sealed class EmployeesQueryHandler(IEmployeeRepository repository) : IQueryHandler<EmployeesQuery, ApiResult<object>>
{
    public async Task<ApiResult<object>> Handle(EmployeesQuery request, CancellationToken ct)
    {
        var rows = await repository.ListAsync(value => true, ct);
        return ApiResultBuilder.Success<object>(rows);
    }
}