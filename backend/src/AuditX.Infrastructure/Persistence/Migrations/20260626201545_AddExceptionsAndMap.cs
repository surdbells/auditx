using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExceptionsAndMap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exceptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    audit_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    checklist_item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    auditable_entity_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    root_cause = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    recommendation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    owner_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    raised_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    raised_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    target_date = table.Column<DateOnly>(type: "date", nullable: false),
                    target_date_overridden = table.Column<bool>(type: "bit", nullable: false),
                    target_date_override_rationale = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    is_recurrence = table.Column<bool>(type: "bit", nullable: false),
                    recurrence_of_exception_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    cia_pending = table.Column<bool>(type: "bit", nullable: false),
                    configuration_versions_json = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    closure_evidence_note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    closed_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    cia_countersigned_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    cia_countersigned_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    map_rejection_reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    map_submitted_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    map_submitted_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    map_approved_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    map_approved_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    cancellation_reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exceptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_exceptions_audit_checklist_items_checklist_item_id",
                        column: x => x.checklist_item_id,
                        principalTable: "audit_checklist_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "map_actions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    exception_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    target_date = table.Column<DateOnly>(type: "date", nullable: false),
                    expected_evidence_type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    completed_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_map_actions", x => x.id);
                    table.ForeignKey(
                        name: "FK_map_actions_exceptions_exception_id",
                        column: x => x.exception_id,
                        principalTable: "exceptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exceptions_audit_id",
                table: "exceptions",
                column: "audit_id");

            migrationBuilder.CreateIndex(
                name: "IX_exceptions_auditable_entity_id_category_status",
                table: "exceptions",
                columns: new[] { "auditable_entity_id", "category", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_exceptions_checklist_item_id",
                table: "exceptions",
                column: "checklist_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_exceptions_owner_user_id",
                table: "exceptions",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_exceptions_status_severity_target_date",
                table: "exceptions",
                columns: new[] { "status", "severity", "target_date" });

            migrationBuilder.CreateIndex(
                name: "IX_map_actions_exception_id",
                table: "map_actions",
                column: "exception_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "map_actions");

            migrationBuilder.DropTable(
                name: "exceptions");
        }
    }
}
