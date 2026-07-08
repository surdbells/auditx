using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_sections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    audit_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    order_index = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_sections", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_sections_audits_audit_id",
                        column: x => x.audit_id,
                        principalTable: "audits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_sections_audit_id",
                table: "audit_sections",
                column: "audit_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_sections_audit_id_name",
                table: "audit_sections",
                columns: new[] { "audit_id", "name" },
                unique: true);

            // Back-fill first-class sections for audits created before sections were entities: one row per
            // distinct (audit, section_name) on existing checklist items, ordered by first item appearance.
            migrationBuilder.Sql(@"
INSERT INTO audit_sections (id, audit_id, name, order_index, created_at, created_by, updated_at, updated_by)
SELECT NEWID(), s.audit_id, s.name,
       ROW_NUMBER() OVER (PARTITION BY s.audit_id ORDER BY s.min_order) - 1,
       SYSDATETIMEOFFSET(), NULL, NULL, NULL
FROM (
    SELECT audit_id, section_name AS name, MIN(order_index) AS min_order
    FROM audit_checklist_items
    WHERE section_name IS NOT NULL AND LTRIM(RTRIM(section_name)) <> ''
    GROUP BY audit_id, section_name
) s;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_sections");
        }
    }
}
