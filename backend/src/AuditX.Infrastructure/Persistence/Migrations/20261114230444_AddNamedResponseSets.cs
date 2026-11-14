using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNamedResponseSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_response_option_sets_response_type",
                table: "response_option_sets");

            migrationBuilder.AddColumn<bool>(
                name: "is_archived",
                table: "response_option_sets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "response_option_sets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_response_option_sets_name",
                table: "response_option_sets",
                column: "name",
                unique: true,
                filter: "[name] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_response_option_sets_response_type",
                table: "response_option_sets",
                column: "response_type",
                unique: true,
                filter: "[name] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_response_option_sets_name",
                table: "response_option_sets");

            migrationBuilder.DropIndex(
                name: "IX_response_option_sets_response_type",
                table: "response_option_sets");

            migrationBuilder.DropColumn(
                name: "is_archived",
                table: "response_option_sets");

            migrationBuilder.DropColumn(
                name: "name",
                table: "response_option_sets");

            migrationBuilder.CreateIndex(
                name: "IX_response_option_sets_response_type",
                table: "response_option_sets",
                column: "response_type",
                unique: true);
        }
    }
}
