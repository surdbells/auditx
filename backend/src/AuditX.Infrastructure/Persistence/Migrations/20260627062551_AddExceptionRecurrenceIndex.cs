using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExceptionRecurrenceIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_exceptions_auditable_entity_id_status_closed_at",
                table: "exceptions",
                columns: new[] { "auditable_entity_id", "status", "closed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_exceptions_auditable_entity_id_status_closed_at",
                table: "exceptions");
        }
    }
}
