using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_procedures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    audit_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    checklist_item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    performed_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    performed_on = table.Column<DateOnly>(type: "date", nullable: false),
                    summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    counterparty = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    population = table.Column<int>(type: "int", nullable: true),
                    sample_size = table.Column<int>(type: "int", nullable: true),
                    items_tested = table.Column<int>(type: "int", nullable: true),
                    exceptions_found = table.Column<int>(type: "int", nullable: true),
                    method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_procedures", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_procedures_audit_checklist_items_checklist_item_id",
                        column: x => x.checklist_item_id,
                        principalTable: "audit_checklist_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_audit_procedures_audits_audit_id",
                        column: x => x.audit_id,
                        principalTable: "audits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_procedures_audit_id",
                table: "audit_procedures",
                column: "audit_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_procedures_checklist_item_id",
                table: "audit_procedures",
                column: "checklist_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_procedures_performed_by_user_id",
                table: "audit_procedures",
                column: "performed_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_procedures");
        }
    }
}
