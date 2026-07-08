using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanAuditLinkIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_plan_items_linked_audit_id",
                table: "plan_items");

            migrationBuilder.DropIndex(
                name: "IX_audits_plan_item_id",
                table: "audits");

            migrationBuilder.AddColumn<bool>(
                name: "allow_audit_launch_before_approval",
                table: "bank_settings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_plan_items_linked_audit_id",
                table: "plan_items",
                column: "linked_audit_id",
                unique: true,
                filter: "[linked_audit_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_audits_plan_item_id",
                table: "audits",
                column: "plan_item_id",
                unique: true,
                filter: "[plan_item_id] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_plan_items_linked_audit_id",
                table: "plan_items");

            migrationBuilder.DropIndex(
                name: "IX_audits_plan_item_id",
                table: "audits");

            migrationBuilder.DropColumn(
                name: "allow_audit_launch_before_approval",
                table: "bank_settings");

            migrationBuilder.CreateIndex(
                name: "IX_plan_items_linked_audit_id",
                table: "plan_items",
                column: "linked_audit_id");

            migrationBuilder.CreateIndex(
                name: "IX_audits_plan_item_id",
                table: "audits",
                column: "plan_item_id");
        }
    }
}
