using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltraSol.Modules.Catalog.Infrastructure.Persistence.Migrations;

public partial class AddCollectionStatus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "status",
            schema: "catalog",
            table: "collections",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        // Existing non-archived collections were visible before explicit publishing existed.
        migrationBuilder.Sql("UPDATE catalog.collections SET status = CASE WHEN is_archived THEN 4 ELSE 2 END;");
        migrationBuilder.DropColumn(name: "is_archived", schema: "catalog", table: "collections");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_archived",
            schema: "catalog",
            table: "collections",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql("UPDATE catalog.collections SET is_archived = (status = 4);");
        migrationBuilder.DropColumn(name: "status", schema: "catalog", table: "collections");
    }
}