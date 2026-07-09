using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticsSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analytics_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    metric_key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    dimension = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    value = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    as_of_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analytics_snapshots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_analytics_snapshots_as_of_date",
                table: "analytics_snapshots",
                column: "as_of_date");

            migrationBuilder.CreateIndex(
                name: "IX_analytics_snapshots_metric_key_dimension_as_of_date",
                table: "analytics_snapshots",
                columns: new[] { "metric_key", "dimension", "as_of_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analytics_snapshots");
        }
    }
}
