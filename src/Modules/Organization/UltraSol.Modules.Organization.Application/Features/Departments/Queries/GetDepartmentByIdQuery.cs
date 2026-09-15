using UltraSol.Modules.Organization.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using static UltraSol.Modules.Organization.Application.Features.Departments.Queries.GetDepartmentByIdQuery;

namespace UltraSol.Modules.Organization.Application.Features.Departments.Queries;

public sealed record GetDepartmentByIdQuery(Guid DepartmentId) : IQuery<ApiResult<Response>>
{
    public sealed record Response(Guid Id, string Name, IList<DepartmentChildren>? Children, DepartmentParent? Parent, int EmployeeCount);
    public sealed record DepartmentChildren(Guid Id, string Name);
    public sealed record DepartmentParent(Guid Id, string Name);
}
internal sealed class GetDepartmentByIdQueryHandler(IDepartmentRepository query) : IQueryHandler<GetDepartmentByIdQuery, ApiResult<Response>>
{
    public async Task<ApiResult<Response>> Handle(GetDepartmentByIdQuery request, CancellationToken ct)
    {
        var department = await query.GetByIdAsync(request.DepartmentId, ct);
        if (department is null)
        {
            return ApiResultBuilder.NotFound<Response>();
        }
        var parent = department.ParentId.HasValue ? await query.GetByIdAsync(department.ParentId.Value, ct) : null;
        var parentResponse = parent is not null ? new DepartmentParent(parent.Id, parent.Name) : null;
        var children = department.Children.Select(c => new DepartmentChildren(c.Id, c.Name)).ToList();
        var response = new Response(department.Id, department.Name, children, parentResponse, department.Employees.Count);
        return ApiResultBuilder.Success(response);
    }
}