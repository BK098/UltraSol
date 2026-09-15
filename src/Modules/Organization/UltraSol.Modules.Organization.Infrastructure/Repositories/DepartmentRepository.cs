using MassTransit;
using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Organization.Domain.Departments;
using UltraSol.Modules.Organization.Domain.Repositories;
using UltraSol.Modules.Organization.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Repositories;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static UltraSol.Modules.Organization.Application.Features.Departments.Queries.GetDepartmentTreeQuery;
namespace UltraSol.Modules.Organization.Infrastructure.Repositories;

public sealed class DepartmentRepository(OrganizationDbContext context) : Repository<Department>(context), IDepartmentRepository
{
    public override Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var department = context.Departments.Where(d => d.Id == id && d.IsActive)
            .Include(d => d.Children)
            .Include(d => d.Employees)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        return department;
    }
    public async Task<IReadOnlyList<Department>> GetOrganizationTreeAsync(
        Guid departmentId,
        CancellationToken ct)
    {
        return await context.Departments
    .FromSqlInterpolated($"""
        WITH RECURSIVE ancestors AS
        (
            SELECT
                d."Id",
                d."Name",
                d."ParentId",
                d."IsActive",
                d."ConcurrencyStamp",
                d."CreatedAt",
                d."CreatedBy",
                d."UpdatedAt",
                d."UpdatedBy",
                d."Version",
                d.xmin
            FROM organization.departments d
            WHERE d."Id" = {departmentId}

            UNION ALL

            SELECT
                parent."Id",
                parent."Name",
                parent."ParentId",
                parent."IsActive",
                parent."ConcurrencyStamp",
                parent."CreatedAt",
                parent."CreatedBy",
                parent."UpdatedAt",
                parent."UpdatedBy",
                parent."Version",
                parent.xmin
            FROM organization.departments parent
            INNER JOIN ancestors child
                ON child."ParentId" = parent."Id"
        ),
        root AS
        (
            SELECT *
            FROM ancestors
            WHERE "ParentId" IS NULL
            LIMIT 1
        ),
        department_tree AS
        (
            SELECT *
            FROM root

            UNION ALL

            SELECT
                child."Id",
                child."Name",
                child."ParentId",
                child."IsActive",
                child."ConcurrencyStamp",
                child."CreatedAt",
                child."CreatedBy",
                child."UpdatedAt",
                child."UpdatedBy",
                child."Version",
                child.xmin
            FROM organization.departments child
            INNER JOIN department_tree parent
                ON child."ParentId" = parent."Id"
        )
        SELECT *
        FROM department_tree
        """)
    .AsNoTracking()
    .ToListAsync(ct);
    }
}