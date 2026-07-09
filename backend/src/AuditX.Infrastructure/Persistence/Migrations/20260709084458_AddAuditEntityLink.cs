using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditEntityLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "auditable_entity_id",
                table: "audits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_audits_auditable_entity_id",
                table: "audits",
                column: "auditable_entity_id");

            // Backfill the direct link for plan-launched audits from their plan item's entity.
            migrationBuilder.Sql(
                "UPDATE a SET a.auditable_entity_id = pi.entity_id " +
                "FROM audits a INNER JOIN plan_items pi ON pi.id = a.plan_item_id " +
                "WHERE a.plan_item_id IS NOT NULL AND a.auditable_entity_id IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_audits_auditable_entity_id",
                table: "audits");

            migrationBuilder.DropColumn(
                name: "auditable_entity_id",
                table: "audits");
        }
    }
}
