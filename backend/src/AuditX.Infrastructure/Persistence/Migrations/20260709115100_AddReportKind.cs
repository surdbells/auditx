using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reports_audit_id_version_number",
                table: "reports");

            migrationBuilder.AlterColumn<Guid>(
                name: "audit_id",
                table: "reports",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            // Every existing report row is a per-audit engagement report; backfill the discriminator accordingly (the
            // enum persists as snake_case, so the default is "audit_engagement", not 0/"").
            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "reports",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "audit_engagement");

            migrationBuilder.CreateIndex(
                name: "IX_reports_audit_id_version_number",
                table: "reports",
                columns: new[] { "audit_id", "version_number" },
                unique: true,
                filter: "[audit_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_reports_kind_version_number",
                table: "reports",
                columns: new[] { "kind", "version_number" },
                unique: true,
                filter: "[audit_id] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reports_audit_id_version_number",
                table: "reports");

            migrationBuilder.DropIndex(
                name: "IX_reports_kind_version_number",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "reports");

            migrationBuilder.AlterColumn<Guid>(
                name: "audit_id",
                table: "reports",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_reports_audit_id_version_number",
                table: "reports",
                columns: new[] { "audit_id", "version_number" },
                unique: true);
        }
    }
}
