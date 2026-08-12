using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditKickoffMeeting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "kickoff_agenda",
                table: "audits",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kickoff_location",
                table: "audits",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "kickoff_scheduled_at_utc",
                table: "audits",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "kickoff_agenda",
                table: "audits");

            migrationBuilder.DropColumn(
                name: "kickoff_location",
                table: "audits");

            migrationBuilder.DropColumn(
                name: "kickoff_scheduled_at_utc",
                table: "audits");
        }
    }
}
