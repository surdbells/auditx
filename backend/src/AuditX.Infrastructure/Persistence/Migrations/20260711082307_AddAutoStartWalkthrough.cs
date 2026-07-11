using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoStartWalkthrough : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Default TRUE so the existing settings row keeps the current auto-start behaviour.
            migrationBuilder.AddColumn<bool>(
                name: "auto_start_walkthrough",
                table: "bank_settings",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "auto_start_walkthrough",
                table: "bank_settings");
        }
    }
}
