using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltraSol.Modules.Catalog.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CatalogDbContext))]
[Migration("20260910100000_AddBrandStateAndLogo")]
public partial class AddBrandStateAndLogo : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_active",
            schema: "catalog",
            table: "brands",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<string>(
            name: "logo_url",
            schema: "catalog",
            table: "brands",
            type: "text",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "is_active", schema: "catalog", table: "brands");
        migrationBuilder.DropColumn(name: "logo_url", schema: "catalog", table: "brands");
    }
}
