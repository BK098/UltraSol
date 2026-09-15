using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltraSol.Modules.Organization.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OrganizationIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "organization",
                table: "employees",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyStamp",
                schema: "organization",
                table: "employees",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                schema: "organization",
                table: "employees",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "organization",
                table: "employees",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "organization",
                table: "employees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                schema: "organization",
                table: "employees",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "PreviousIsActive",
                schema: "organization",
                table: "employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SyncError",
                schema: "organization",
                table: "employees",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SyncStatus",
                schema: "organization",
                table: "employees",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "organization",
                table: "employees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                schema: "organization",
                table: "employees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "organization",
                table: "employees",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyStamp",
                schema: "organization",
                table: "departments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "organization",
                table: "departments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "organization",
                table: "departments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "organization",
                table: "departments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                schema: "organization",
                table: "departments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "organization",
                table: "departments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "inbox",
                schema: "organization",
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
                schema: "organization",
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
                name: "IX_outbox_PublishedAt_CreatedAt",
                schema: "organization",
                table: "outbox",
                columns: new[] { "PublishedAt", "CreatedAt" });
            migrationBuilder.Sql("""
                UPDATE organization.employees SET "Version" = 1, "PreviousIsActive" = "IsActive",
                    "SyncStatus" = CASE WHEN "UserId" IS NULL THEN 'Rejected' ELSE 'Completed' END,
                    "CorrelationId" = gen_random_uuid(), "ConcurrencyStamp" = gen_random_uuid()::text, "CreatedAt" = now();
                UPDATE organization.departments SET "Version" = 1, "ConcurrencyStamp" = gen_random_uuid()::text, "CreatedAt" = now();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "outbox",
                schema: "organization");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "organization",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                schema: "organization",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "organization",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "organization",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "Email",
                schema: "organization",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "PreviousIsActive",
                schema: "organization",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "SyncError",
                schema: "organization",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "SyncStatus",
                schema: "organization",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "organization",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                schema: "organization",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "organization",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "organization",
                table: "departments");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "organization",
                table: "departments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "organization",
                table: "departments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "organization",
                table: "departments");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                schema: "organization",
                table: "departments");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "organization",
                table: "departments");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "organization",
                table: "employees",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}