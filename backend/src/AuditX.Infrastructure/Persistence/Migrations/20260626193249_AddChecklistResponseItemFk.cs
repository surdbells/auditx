using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChecklistResponseItemFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_checklist_responses_checklist_item_id",
                table: "checklist_responses",
                column: "checklist_item_id");

            migrationBuilder.AddForeignKey(
                name: "FK_checklist_responses_audit_checklist_items_checklist_item_id",
                table: "checklist_responses",
                column: "checklist_item_id",
                principalTable: "audit_checklist_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_checklist_responses_audit_checklist_items_checklist_item_id",
                table: "checklist_responses");

            migrationBuilder.DropIndex(
                name: "IX_checklist_responses_checklist_item_id",
                table: "checklist_responses");
        }
    }
}
