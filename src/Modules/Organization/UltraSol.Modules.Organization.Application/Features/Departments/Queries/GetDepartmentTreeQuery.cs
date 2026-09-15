using UltraSol.Modules.Organization.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using static UltraSol.Modules.Organization.Application.Features.Departments.Queries.GetDepartmentTreeQuery;

namespace UltraSol.Modules.Organization.Application.Features.Departments.Queries;

public sealed record GetDepartmentTreeQuery(Guid DepartmentId) : IQuery<ApiResult<Response>>
{
    public sealed record Response(
        Guid CurrentDepartmentId,
        Guid RootDepartmentId,
        string RootDepartmentName,
        int DepartmentCount,
        DepartmentNode Tree);

    public sealed record DepartmentNode(
        Guid Id,
        string Name,
        Guid? ParentId,
        IList<DepartmentNode> Children);
}
internal sealed class GetDepartmentTreeQueryHandler(
    IDepartmentRepository repository)
    : IQueryHandler<GetDepartmentTreeQuery, ApiResult<GetDepartmentTreeQuery.Response>>
{
    public async Task<ApiResult<GetDepartmentTreeQuery.Response>> Handle(
        GetDepartmentTreeQuery request,
        CancellationToken ct)
    {
        var departments = await repository.GetOrganizationTreeAsync(request.DepartmentId, ct);

        if (departments.Count == 0)
        {
            return ApiResultBuilder.NotFound<GetDepartmentTreeQuery.Response>();
        }

        var currentDepartment = departments.FirstOrDefault(x => x.Id == request.DepartmentId);

        if (currentDepartment is null)
        {
            return ApiResultBuilder.NotFound<GetDepartmentTreeQuery.Response>();
        }

        var root = departments.FirstOrDefault(x => x.ParentId is null);

        if (root is null)
        {
            return ApiResultBuilder.NotFound<GetDepartmentTreeQuery.Response>();
        }

        var departmentById = departments.ToDictionary(x => x.Id);

        var childrenByParentId = departments
            .Where(x => x.ParentId.HasValue)
            .GroupBy(x => x.ParentId!.Value)
            .ToDictionary(
                x => x.Key,
                x => x.ToList());

        var tree = BuildTree(
            root.Id,
            departmentById,
            childrenByParentId);

        var response = new GetDepartmentTreeQuery.Response(
            CurrentDepartmentId: currentDepartment.Id,
            RootDepartmentId: root.Id,
            RootDepartmentName: root.Name,
            DepartmentCount: departments.Count,
            Tree: tree);

        return ApiResultBuilder.Success(response);
    }

    private static GetDepartmentTreeQuery.DepartmentNode BuildTree(
        Guid departmentId,
        IReadOnlyDictionary<Guid, Domain.Departments.Department> departmentById,
        IReadOnlyDictionary<Guid, List<Domain.Departments.Department>> childrenByParentId)
    {
        var department = departmentById[departmentId];

        var children = childrenByParentId.TryGetValue(departmentId, out var childDepartments)
            ? childDepartments
                .Select(x => BuildTree(
                    x.Id,
                    departmentById,
                    childrenByParentId))
                .ToList()
            : [];

        return new GetDepartmentTreeQuery.DepartmentNode(
            Id: department.Id,
            Name: department.Name,
            ParentId: department.ParentId,
            Children: children);
    }
}