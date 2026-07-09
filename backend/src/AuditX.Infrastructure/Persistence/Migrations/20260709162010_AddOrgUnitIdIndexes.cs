using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgUnitIdIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_users_org_unit_id",
                table: "users",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_universe_entities_org_unit_id",
                table: "audit_universe_entities",
                column: "org_unit_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_org_unit_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_audit_universe_entities_org_unit_id",
                table: "audit_universe_entities");
        }
    }
}
