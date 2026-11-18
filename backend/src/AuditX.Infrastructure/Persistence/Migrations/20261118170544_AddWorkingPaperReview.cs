using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkingPaperReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reviewed_at",
                table: "audit_checklist_items",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reviewed_by",
                table: "audit_checklist_items",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "review_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    audit_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    checklist_item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    raised_by = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    raised_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    answer = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    answered_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    answered_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    cleared_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    cleared_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_notes", x => x.id);
                    table.ForeignKey(
                        name: "FK_review_notes_audit_checklist_items_checklist_item_id",
                        column: x => x.checklist_item_id,
                        principalTable: "audit_checklist_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_review_notes_audits_audit_id",
                        column: x => x.audit_id,
                        principalTable: "audits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_review_notes_audit_id_status",
                table: "review_notes",
                columns: new[] { "audit_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_review_notes_checklist_item_id",
                table: "review_notes",
                column: "checklist_item_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "review_notes");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                table: "audit_checklist_items");

            migrationBuilder.DropColumn(
                name: "reviewed_by",
                table: "audit_checklist_items");
        }
    }
}
