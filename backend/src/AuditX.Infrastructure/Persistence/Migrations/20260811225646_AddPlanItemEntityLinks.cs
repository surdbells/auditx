using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanItemEntityLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_plan_items_entity_id",
                table: "plan_items");

            migrationBuilder.DropIndex(
                name: "IX_plan_items_linked_audit_id",
                table: "plan_items");

            migrationBuilder.DropColumn(
                name: "entity_id",
                table: "plan_items");

            migrationBuilder.DropColumn(
                name: "linked_audit_id",
                table: "plan_items");

            migrationBuilder.DropColumn(
                name: "status",
                table: "plan_items");

            migrationBuilder.CreateTable(
                name: "plan_item_entity_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    plan_item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    entity_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    linked_audit_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_item_entity_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_plan_item_entity_links_plan_items_plan_item_id",
                        column: x => x.plan_item_id,
                        principalTable: "plan_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_plan_item_entity_links_entity_id",
                table: "plan_item_entity_links",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "IX_plan_item_entity_links_linked_audit_id",
                table: "plan_item_entity_links",
                column: "linked_audit_id",
                unique: true,
                filter: "[linked_audit_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_plan_item_entity_links_plan_item_id_entity_id",
                table: "plan_item_entity_links",
                columns: new[] { "plan_item_id", "entity_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plan_item_entity_links");

            migrationBuilder.AddColumn<Guid>(
                name: "entity_id",
                table: "plan_items",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "linked_audit_id",
                table: "plan_items",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "plan_items",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_plan_items_entity_id",
                table: "plan_items",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "IX_plan_items_linked_audit_id",
                table: "plan_items",
                column: "linked_audit_id",
                unique: true,
                filter: "[linked_audit_id] IS NOT NULL");
        }
    }
}
