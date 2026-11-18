using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportSignOffAndComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reports issued before draft/final review existed were final reports without a sign-off step: they keep
            // stage "final" and sign-off "not_required", so they stay distributable exactly as before.
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "sign_off_at",
                table: "reports",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "sign_off_by",
                table: "reports",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sign_off_note",
                table: "reports",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sign_off_status",
                table: "reports",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "not_required");

            migrationBuilder.AddColumn<string>(
                name: "stage",
                table: "reports",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "final");

            migrationBuilder.CreateTable(
                name: "report_comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    report_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    report_version_number = table.Column<int>(type: "int", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    posted_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_comments", x => x.id);
                    table.ForeignKey(
                        name: "FK_report_comments_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reports_sign_off_status",
                table: "reports",
                column: "sign_off_status");

            migrationBuilder.CreateIndex(
                name: "IX_report_comments_report_id_posted_at",
                table: "report_comments",
                columns: new[] { "report_id", "posted_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "report_comments");

            migrationBuilder.DropIndex(
                name: "IX_reports_sign_off_status",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "sign_off_at",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "sign_off_by",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "sign_off_note",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "sign_off_status",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "stage",
                table: "reports");
        }
    }
}
