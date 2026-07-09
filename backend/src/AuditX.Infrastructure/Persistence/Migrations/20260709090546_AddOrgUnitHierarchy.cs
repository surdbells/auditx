using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgUnitHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "org_unit_id",
                table: "users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "org_unit_id",
                table: "audit_universe_entities",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "org_units",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    parent_org_unit_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    is_archived = table.Column<bool>(type: "bit", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_units", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_org_units_code",
                table: "org_units",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_org_units_parent_org_unit_id",
                table: "org_units",
                column: "parent_org_unit_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "org_units");

            migrationBuilder.DropColumn(
                name: "org_unit_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "org_unit_id",
                table: "audit_universe_entities");
        }
    }
}
