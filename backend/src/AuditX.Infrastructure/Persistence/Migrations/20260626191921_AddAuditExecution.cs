using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "require_comment_on_pass",
                table: "bank_settings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fail_judged_at",
                table: "audit_checklist_items",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "fail_judged_by",
                table: "audit_checklist_items",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fail_justification",
                table: "audit_checklist_items",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_exception",
                table: "audit_checklist_items",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "checklist_responses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    audit_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    checklist_item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    verdict = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    responder_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    is_draft = table.Column<bool>(type: "bit", nullable: false),
                    response_version = table.Column<int>(type: "int", nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checklist_responses", x => x.id);
                    table.ForeignKey(
                        name: "FK_checklist_responses_audits_audit_id",
                        column: x => x.audit_id,
                        principalTable: "audits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "evidence_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    audit_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    context_type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    context_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    storage_path = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    original_filename = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    mime_type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256_hash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    is_flagged = table.Column<bool>(type: "bit", nullable: false),
                    deletion_reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence_files", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_checklist_responses_audit_id",
                table: "checklist_responses",
                column: "audit_id");

            migrationBuilder.CreateIndex(
                name: "IX_checklist_responses_audit_id_checklist_item_id",
                table: "checklist_responses",
                columns: new[] { "audit_id", "checklist_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evidence_files_audit_id",
                table: "evidence_files",
                column: "audit_id");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_files_audit_id_context_type_context_id",
                table: "evidence_files",
                columns: new[] { "audit_id", "context_type", "context_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "checklist_responses");

            migrationBuilder.DropTable(
                name: "evidence_files");

            migrationBuilder.DropColumn(
                name: "require_comment_on_pass",
                table: "bank_settings");

            migrationBuilder.DropColumn(
                name: "fail_judged_at",
                table: "audit_checklist_items");

            migrationBuilder.DropColumn(
                name: "fail_judged_by",
                table: "audit_checklist_items");

            migrationBuilder.DropColumn(
                name: "fail_justification",
                table: "audit_checklist_items");

            migrationBuilder.DropColumn(
                name: "has_exception",
                table: "audit_checklist_items");
        }
    }
}
