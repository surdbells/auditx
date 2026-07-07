using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanItemOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "order_index",
                table: "plan_items",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Backfill a stable per-plan order for pre-existing items (by planned start date, then id)
            // so manual ordering starts from a sensible sequence rather than all-zeros.
            migrationBuilder.Sql(
                """
                WITH ordered AS (
                    SELECT id,
                           ROW_NUMBER() OVER (PARTITION BY annual_plan_id ORDER BY planned_start_date, id) - 1 AS seq
                    FROM plan_items
                )
                UPDATE pi SET order_index = o.seq
                FROM plan_items pi
                INNER JOIN ordered o ON o.id = pi.id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "order_index",
                table: "plan_items");
        }
    }
}
