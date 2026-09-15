using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltraSol.Modules.Auth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAuthorizationScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Retire scoped grants instead of silently turning them into global permissions.
            migrationBuilder.Sql("""
                DELETE FROM auth.role_assignments WHERE cardinality("ScopeIds") > 0;
                DELETE FROM auth.direct_permissions WHERE cardinality("ScopeIds") > 0;
                DELETE FROM auth.outbox WHERE "Type" = 'UltraSol.Shared.IntegrationEvents.Auth.ProductScopesChanged';
                UPDATE auth.authorization_version SET "Version" = "Version" + 1 WHERE "Id" = 1;
                """);

            migrationBuilder.DropTable(
                name: "resource_scopes",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "scopes",
                schema: "auth");

            migrationBuilder.DropColumn(
                name: "ScopeIds",
                schema: "auth",
                table: "role_assignments");

            migrationBuilder.DropColumn(
                name: "ScopeIds",
                schema: "auth",
                table: "direct_permissions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid[]>(
                name: "ScopeIds",
                schema: "auth",
                table: "role_assignments",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.AddColumn<Guid[]>(
                name: "ScopeIds",
                schema: "auth",
                table: "direct_permissions",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.CreateTable(
                name: "scopes",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SourceVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "resource_scopes",
                schema: "auth",
                columns: table => new
                {
                    ResourceType = table.Column<string>(type: "text", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resource_scopes", x => new { x.ResourceType, x.ResourceId, x.ScopeId });
                    table.ForeignKey(
                        name: "FK_resource_scopes_scopes_ScopeId",
                        column: x => x.ScopeId,
                        principalSchema: "auth",
                        principalTable: "scopes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_resource_scopes_ScopeId",
                schema: "auth",
                table: "resource_scopes",
                column: "ScopeId");

            migrationBuilder.CreateIndex(
                name: "IX_scopes_DepartmentId",
                schema: "auth",
                table: "scopes",
                column: "DepartmentId",
                unique: true);
        }
    }
}