using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProgrammeApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "require_programme_approval",
                table: "institution_settings",
                type: "bit",
                nullable: false,
                // On for existing installations too: fieldwork waits for a manager to approve the audit programme.
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "objectives",
                table: "audits",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "programme_approval_note",
                table: "audits",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "programme_approved_at",
                table: "audits",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "programme_approved_by",
                table: "audits",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "require_programme_approval",
                table: "institution_settings");

            migrationBuilder.DropColumn(
                name: "objectives",
                table: "audits");

            migrationBuilder.DropColumn(
                name: "programme_approval_note",
                table: "audits");

            migrationBuilder.DropColumn(
                name: "programme_approved_at",
                table: "audits");

            migrationBuilder.DropColumn(
                name: "programme_approved_by",
                table: "audits");
        }
    }
}
