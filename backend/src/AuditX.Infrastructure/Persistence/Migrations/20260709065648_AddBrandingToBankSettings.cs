using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandingToBankSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "accent_color",
                table: "bank_settings",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "#7c3aed");

            migrationBuilder.AddColumn<string>(
                name: "icon_data_uri",
                table: "bank_settings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "logo_data_uri",
                table: "bank_settings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "primary_color",
                table: "bank_settings",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "#4f46e5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "accent_color",
                table: "bank_settings");

            migrationBuilder.DropColumn(
                name: "icon_data_uri",
                table: "bank_settings");

            migrationBuilder.DropColumn(
                name: "logo_data_uri",
                table: "bank_settings");

            migrationBuilder.DropColumn(
                name: "primary_color",
                table: "bank_settings");
        }
    }
}
