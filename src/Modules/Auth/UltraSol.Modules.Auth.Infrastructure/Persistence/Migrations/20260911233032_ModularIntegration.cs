using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltraSol.Modules.Auth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ModularIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SourceVersion",
                schema: "auth",
                table: "scopes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "employee_accounts",
                schema: "auth",
                columns: table => new
                {
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_accounts", x => x.EmployeeId);
                });

            migrationBuilder.CreateTable(
                name: "inbox",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_employee_accounts_UserId",
                schema: "auth",
                table: "employee_accounts",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_PublishedAt_CreatedAt",
                schema: "auth",
                table: "outbox",
                columns: new[] { "PublishedAt", "CreatedAt" });

            migrationBuilder.Sql("""
                INSERT INTO auth.permissions ("Code", "RequestType", "IsActive")
                SELECT replace("Code", 'Organization.Departments.', 'Organization.Employees.'),
                       replace("RequestType", '.Features.Departments.', '.Features.Employees.'), "IsActive"
                FROM auth.permissions WHERE "Code" IN ('Organization.Departments.Employees', 'Organization.Departments.CreateEmployee', 'Organization.Departments.UpdateEmployee')
                ON CONFLICT ("Code") DO NOTHING;
                INSERT INTO auth.role_permissions ("RoleId", "PermissionCode", "Deny")
                SELECT "RoleId", replace("PermissionCode", 'Organization.Departments.', 'Organization.Employees.'), "Deny"
                FROM auth.role_permissions WHERE "PermissionCode" IN ('Organization.Departments.Employees', 'Organization.Departments.CreateEmployee', 'Organization.Departments.UpdateEmployee')
                ON CONFLICT ("RoleId", "PermissionCode") DO UPDATE SET "Deny" = auth.role_permissions."Deny" OR EXCLUDED."Deny";
                DELETE FROM auth.role_permissions WHERE "PermissionCode" IN ('Organization.Departments.Employees', 'Organization.Departments.CreateEmployee', 'Organization.Departments.UpdateEmployee');
                UPDATE auth.direct_permissions SET "PermissionCode" = replace("PermissionCode", 'Organization.Departments.', 'Organization.Employees.')
                WHERE "PermissionCode" IN ('Organization.Departments.Employees', 'Organization.Departments.CreateEmployee', 'Organization.Departments.UpdateEmployee');
                UPDATE auth.permissions SET "IsActive" = false WHERE "Code" IN ('Organization.Departments.Employees', 'Organization.Departments.CreateEmployee', 'Organization.Departments.UpdateEmployee');

                CREATE TEMP TABLE auth_scope_remap ON COMMIT DROP AS
                    SELECT "Id" AS old_id, "DepartmentId" AS new_id FROM auth.scopes WHERE "DepartmentId" IS NOT NULL AND "Id" <> "DepartmentId";
                UPDATE auth.scopes SET "DepartmentId" = NULL WHERE "Id" IN (SELECT old_id FROM auth_scope_remap);
                INSERT INTO auth.scopes ("Id", "Name", "DepartmentId", "SourceVersion")
                    SELECT r.new_id, s."Name", r.new_id, s."SourceVersion" FROM auth.scopes s JOIN auth_scope_remap r ON s."Id" = r.old_id;
                UPDATE auth.role_assignments SET "ScopeIds" = ARRAY(SELECT coalesce(r.new_id, scope_id) FROM unnest("ScopeIds") scope_id LEFT JOIN auth_scope_remap r ON r.old_id = scope_id);
                UPDATE auth.direct_permissions SET "ScopeIds" = ARRAY(SELECT coalesce(r.new_id, scope_id) FROM unnest("ScopeIds") scope_id LEFT JOIN auth_scope_remap r ON r.old_id = scope_id);
                INSERT INTO auth.resource_scopes ("ResourceType", "ResourceId", "ScopeId")
                    SELECT s."ResourceType", s."ResourceId", r.new_id FROM auth.resource_scopes s JOIN auth_scope_remap r ON s."ScopeId" = r.old_id ON CONFLICT DO NOTHING;
                DELETE FROM auth.resource_scopes WHERE "ScopeId" IN (SELECT old_id FROM auth_scope_remap);
                DELETE FROM auth.scopes WHERE "Id" IN (SELECT old_id FROM auth_scope_remap);
                UPDATE auth.authorization_version SET "Version" = "Version" + 1 WHERE "Id" = 1;
                INSERT INTO auth.outbox ("Id", "Type", "Payload", "CreatedAt")
                    SELECT gen_random_uuid(), 'UltraSol.Shared.IntegrationEvents.Auth.ProductScopesChanged',
                        json_build_object('EventId', gen_random_uuid(), 'CorrelationId', gen_random_uuid(), 'ContractVersion', 1,
                            'AggregateVersion', (SELECT "Version" FROM auth.authorization_version WHERE "Id" = 1),
                            'ProductId', "ResourceId", 'ScopeIds', array_agg("ScopeId"))::text, now()
                    FROM auth.resource_scopes WHERE "ResourceType" = 'Product' GROUP BY "ResourceId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_accounts",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "inbox",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "outbox",
                schema: "auth");

            migrationBuilder.DropColumn(
                name: "SourceVersion",
                schema: "auth",
                table: "scopes");
        }
    }
}