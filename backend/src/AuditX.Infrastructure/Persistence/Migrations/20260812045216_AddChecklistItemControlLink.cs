using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChecklistItemControlLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "control_id",
                table: "template_items",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "control_id",
                table: "audit_checklist_items",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_template_items_control_id",
                table: "template_items",
                column: "control_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_checklist_items_control_id",
                table: "audit_checklist_items",
                column: "control_id");

            migrationBuilder.AddForeignKey(
                name: "FK_audit_checklist_items_controls_control_id",
                table: "audit_checklist_items",
                column: "control_id",
                principalTable: "controls",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_template_items_controls_control_id",
                table: "template_items",
                column: "control_id",
                principalTable: "controls",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_checklist_items_controls_control_id",
                table: "audit_checklist_items");

            migrationBuilder.DropForeignKey(
                name: "FK_template_items_controls_control_id",
                table: "template_items");

            migrationBuilder.DropIndex(
                name: "IX_template_items_control_id",
                table: "template_items");

            migrationBuilder.DropIndex(
                name: "IX_audit_checklist_items_control_id",
                table: "audit_checklist_items");

            migrationBuilder.DropColumn(
                name: "control_id",
                table: "template_items");

            migrationBuilder.DropColumn(
                name: "control_id",
                table: "audit_checklist_items");
        }
    }
}
