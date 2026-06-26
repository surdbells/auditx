using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniverseAndPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "annual_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    period_label = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    approval_decision_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_annual_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_universe_entities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    entity_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    parent_entity_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    owner_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    inherent_risk_scores_json = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    residual_risk_scores_json = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    composite_inherent_score = table.Column<decimal>(type: "decimal(6,3)", nullable: true),
                    composite_residual_score = table.Column<decimal>(type: "decimal(6,3)", nullable: true),
                    last_audited_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
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
                    table.PrimaryKey("PK_audit_universe_entities", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_universe_entities_audit_universe_entities_parent_entity_id",
                        column: x => x.parent_entity_id,
                        principalTable: "audit_universe_entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "entity_type_taxonomy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_type_taxonomy", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "risk_dimensions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    weight = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    scale_min = table.Column<int>(type: "int", nullable: false),
                    scale_max = table.Column<int>(type: "int", nullable: false),
                    scale_label_overrides_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_dimensions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plan_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    annual_plan_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    entity_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    audit_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    planned_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    planned_end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    estimated_effort_days = table.Column<decimal>(type: "decimal(6,1)", nullable: true),
                    assigned_lead_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    linked_audit_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_plan_items_annual_plans_annual_plan_id",
                        column: x => x.annual_plan_id,
                        principalTable: "annual_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_annual_plans_period_start_period_end",
                table: "annual_plans",
                columns: new[] { "period_start", "period_end" });

            migrationBuilder.CreateIndex(
                name: "IX_annual_plans_status",
                table: "annual_plans",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_audit_universe_entities_entity_type_is_deleted",
                table: "audit_universe_entities",
                columns: new[] { "entity_type", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_universe_entities_entity_type_last_audited_at",
                table: "audit_universe_entities",
                columns: new[] { "entity_type", "last_audited_at" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_universe_entities_name",
                table: "audit_universe_entities",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_audit_universe_entities_parent_entity_id",
                table: "audit_universe_entities",
                column: "parent_entity_id");

            migrationBuilder.CreateIndex(
                name: "IX_entity_type_taxonomy_name",
                table: "entity_type_taxonomy",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plan_items_annual_plan_id_planned_start_date",
                table: "plan_items",
                columns: new[] { "annual_plan_id", "planned_start_date" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_items_entity_id",
                table: "plan_items",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "IX_plan_items_linked_audit_id",
                table: "plan_items",
                column: "linked_audit_id");

            migrationBuilder.CreateIndex(
                name: "IX_risk_dimensions_name",
                table: "risk_dimensions",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_universe_entities");

            migrationBuilder.DropTable(
                name: "entity_type_taxonomy");

            migrationBuilder.DropTable(
                name: "plan_items");

            migrationBuilder.DropTable(
                name: "risk_dimensions");

            migrationBuilder.DropTable(
                name: "annual_plans");
        }
    }
}
