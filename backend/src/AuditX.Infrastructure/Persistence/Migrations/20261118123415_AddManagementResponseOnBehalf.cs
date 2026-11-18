using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManagementResponseOnBehalf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "management_response_on_behalf",
                table: "exceptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Until now only the audit team could record a response, so every existing response that the finding's
            // owner did not record themselves was entered on management's behalf.
            migrationBuilder.Sql(
                "UPDATE exceptions SET management_response_on_behalf = 1 " +
                "WHERE management_response_decision IS NOT NULL AND management_responded_by <> owner_user_id;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "management_response_on_behalf",
                table: "exceptions");
        }
    }
}
