using UltraSol.Modules.Organization.Domain.Departments;
using UltraSol.Modules.Organization.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Extensions;
using UltraSol.Shared.Domain.Common.Paging;
using UltraSol.Shared.Domain.Common.Specifications;
using static UltraSol.Modules.Organization.Application.Features.Departments.Queries.GetDepartmentsQuery;

namespace UltraSol.Modules.Organization.Application.Features.Departments.Queries;

public sealed record GetDepartmentsQuery(PagedFilter? Filter) : IQuery<ApiResult<PaginatedResult<Response>>>
{
    public sealed record Response(Guid Id, string Name);
}
internal sealed class GetDepartmentsQueryHandler(IDepartmentRepository query) : IQueryHandler<GetDepartmentsQuery, ApiResult<PaginatedResult<Response>>>
{
    public async Task<ApiResult<PaginatedResult<Response>>> Handle(GetDepartmentsQuery request, CancellationToken ct)
    {
        var filter = request.Filter ?? new PagedFilter();
        var spec = Spec.For<Department>(builder => builder
            .WhereIf(
                filter.Search.HasValue(),
                () => department => department.Name.Contains(filter.Search!))
            .OrderBy(department => department.Name)
            .AsNoTracking());
        var page = PaginationRequest.Create(filter.PageIndex, filter.PageSize, SortDescriptor.Asc("Name")) with { Search = filter.Search };
        var result = await query.GetPagedAsync(page, spec, ct).ConfigureAwait(false);
        var response = result.Map(department => new Response(department.Id, department.Name));
        return ApiResultBuilder.Success(response);
    }
}