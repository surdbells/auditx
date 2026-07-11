using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPageGuideVisibilitySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Default TRUE so the existing settings row keeps both page-guide buttons visible (current behaviour).
            migrationBuilder.AddColumn<bool>(
                name: "show_overview",
                table: "bank_settings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "show_walkthrough",
                table: "bank_settings",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "show_overview",
                table: "bank_settings");

            migrationBuilder.DropColumn(
                name: "show_walkthrough",
                table: "bank_settings");
        }
    }
}
