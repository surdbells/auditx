using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAudits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    scope_description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    audit_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    target_end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    actual_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    template_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    template_version = table.Column<int>(type: "int", nullable: true),
                    plan_item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    lead_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    auditee_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    configuration_versions_json = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    cancellation_reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    last_transition_reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audits", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_checklist_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    audit_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    section_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    order_index = table.Column<int>(type: "int", nullable: false),
                    prompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    reference_notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    response_type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    assigned_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    is_required = table.Column<bool>(type: "bit", nullable: false),
                    item_state = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_checklist_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_checklist_items_audits_audit_id",
                        column: x => x.audit_id,
                        principalTable: "audits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_team_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    audit_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    team_role = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    added_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    added_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    removed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_team_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_team_members_audits_audit_id",
                        column: x => x.audit_id,
                        principalTable: "audits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_checklist_items_assigned_user_id",
                table: "audit_checklist_items",
                column: "assigned_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_checklist_items_audit_id_order_index",
                table: "audit_checklist_items",
                columns: new[] { "audit_id", "order_index" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_team_members_audit_id",
                table: "audit_team_members",
                column: "audit_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_team_members_audit_id_user_id",
                table: "audit_team_members",
                columns: new[] { "audit_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_audits_audit_type",
                table: "audits",
                column: "audit_type");

            migrationBuilder.CreateIndex(
                name: "IX_audits_lead_user_id",
                table: "audits",
                column: "lead_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_audits_plan_item_id",
                table: "audits",
                column: "plan_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_audits_status",
                table: "audits",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_audits_status_start_date",
                table: "audits",
                columns: new[] { "status", "start_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_checklist_items");

            migrationBuilder.DropTable(
                name: "audit_team_members");

            migrationBuilder.DropTable(
                name: "audits");
        }
    }
}
