using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "enable_local_passwords",
                table: "institution_settings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "password_expiry_days",
                table: "institution_settings",
                type: "int",
                nullable: false,
                defaultValue: 90);

            migrationBuilder.AddColumn<int>(
                name: "password_history_depth",
                table: "institution_settings",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "password_lockout_minutes",
                table: "institution_settings",
                type: "int",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<int>(
                name: "password_max_failed_attempts",
                table: "institution_settings",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "password_min_length",
                table: "institution_settings",
                type: "int",
                nullable: false,
                defaultValue: 12);

            migrationBuilder.AddColumn<bool>(
                name: "password_require_digit",
                table: "institution_settings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "password_require_lowercase",
                table: "institution_settings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "password_require_symbol",
                table: "institution_settings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "password_require_uppercase",
                table: "institution_settings",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "enable_local_passwords",
                table: "institution_settings");

            migrationBuilder.DropColumn(
                name: "password_expiry_days",
                table: "institution_settings");

            migrationBuilder.DropColumn(
                name: "password_history_depth",
                table: "institution_settings");

            migrationBuilder.DropColumn(
                name: "password_lockout_minutes",
                table: "institution_settings");

            migrationBuilder.DropColumn(
                name: "password_max_failed_attempts",
                table: "institution_settings");

            migrationBuilder.DropColumn(
                name: "password_min_length",
                table: "institution_settings");

            migrationBuilder.DropColumn(
                name: "password_require_digit",
                table: "institution_settings");

            migrationBuilder.DropColumn(
                name: "password_require_lowercase",
                table: "institution_settings");

            migrationBuilder.DropColumn(
                name: "password_require_symbol",
                table: "institution_settings");

            migrationBuilder.DropColumn(
                name: "password_require_uppercase",
                table: "institution_settings");
        }
    }
}
