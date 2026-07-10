using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the unique filtered slug index before changing the column's collation — SQL Server is fragile
            // about altering an indexed column's collation across versions — then recreate it identically after.
            migrationBuilder.DropIndex(name: "IX_shared_links_slug", table: "shared_links");

            migrationBuilder.AlterColumn<string>(
                name: "slug",
                table: "shared_links",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.CreateIndex(
                name: "IX_shared_links_slug",
                table: "shared_links",
                column: "slug",
                unique: true,
                filter: "[is_deleted] = 0");

            migrationBuilder.CreateTable(
                name: "report_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    cadence = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    recipients_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    next_run_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    last_run_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    last_report_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_report_schedules", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_report_schedules_is_active_next_run_at",
                table: "report_schedules",
                columns: new[] { "is_active", "next_run_at" },
                filter: "[is_deleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "report_schedules");

            migrationBuilder.DropIndex(name: "IX_shared_links_slug", table: "shared_links");

            migrationBuilder.AlterColumn<string>(
                name: "slug",
                table: "shared_links",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.CreateIndex(
                name: "IX_shared_links_slug",
                table: "shared_links",
                column: "slug",
                unique: true,
                filter: "[is_deleted] = 0");
        }
    }
}
