using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEngagementRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "require_independence_declaration",
                table: "institution_settings",
                type: "bit",
                nullable: false,
                // On for existing installations too: fieldwork waits for the team's independence declarations.
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "audit_meetings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    audit_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    held_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    attendee_user_ids = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    external_attendees = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    minutes = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    agreed_points = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    report_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    recorded_by = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    last_edited_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    last_edited_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_meetings", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_meetings_audits_audit_id",
                        column: x => x.audit_id,
                        principalTable: "audits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_audit_meetings_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "engagement_letters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    audit_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    body = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    revision = table.Column<int>(type: "int", nullable: false),
                    drafted_by = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    drafted_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    issued_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    issued_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    acknowledged_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    acknowledged_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    acknowledgement_comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_engagement_letters", x => x.id);
                    table.ForeignKey(
                        name: "FK_engagement_letters_audits_audit_id",
                        column: x => x.audit_id,
                        principalTable: "audits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "independence_declarations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    audit_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    has_conflict = table.Column<bool>(type: "bit", nullable: false),
                    conflict_details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    declared_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    review_note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_independence_declarations", x => x.id);
                    table.ForeignKey(
                        name: "FK_independence_declarations_audits_audit_id",
                        column: x => x.audit_id,
                        principalTable: "audits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "engagement_letter_revisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    revision = table.Column<int>(type: "int", nullable: false),
                    body = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: false),
                    issued_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    issued_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    acknowledged_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    acknowledged_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    acknowledgement_comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    engagement_letter_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_engagement_letter_revisions", x => x.id);
                    table.ForeignKey(
                        name: "FK_engagement_letter_revisions_engagement_letters_engagement_letter_id",
                        column: x => x.engagement_letter_id,
                        principalTable: "engagement_letters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_meetings_audit_id_kind",
                table: "audit_meetings",
                columns: new[] { "audit_id", "kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_meetings_report_id",
                table: "audit_meetings",
                column: "report_id");

            migrationBuilder.CreateIndex(
                name: "IX_engagement_letter_revisions_engagement_letter_id_revision",
                table: "engagement_letter_revisions",
                columns: new[] { "engagement_letter_id", "revision" },
                unique: true,
                filter: "[engagement_letter_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_engagement_letters_audit_id",
                table: "engagement_letters",
                column: "audit_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_independence_declarations_audit_id_user_id",
                table: "independence_declarations",
                columns: new[] { "audit_id", "user_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_meetings");

            migrationBuilder.DropTable(
                name: "engagement_letter_revisions");

            migrationBuilder.DropTable(
                name: "independence_declarations");

            migrationBuilder.DropTable(
                name: "engagement_letters");

            migrationBuilder.DropColumn(
                name: "require_independence_declaration",
                table: "institution_settings");
        }
    }
}
