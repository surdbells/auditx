using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdleTimeoutSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Secure defaults for the existing settings row: warn after 15 idle minutes, 60s countdown to logout.
            // Admins can set idle_timeout_minutes to 0 to disable idle logout entirely.
            migrationBuilder.AddColumn<int>(
                name: "idle_timeout_minutes",
                table: "bank_settings",
                type: "int",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<int>(
                name: "idle_warning_seconds",
                table: "bank_settings",
                type: "int",
                nullable: false,
                defaultValue: 60);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "idle_timeout_minutes",
                table: "bank_settings");

            migrationBuilder.DropColumn(
                name: "idle_warning_seconds",
                table: "bank_settings");
        }
    }
}
