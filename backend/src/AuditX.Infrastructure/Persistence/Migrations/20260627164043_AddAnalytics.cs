using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dashboards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    permission_required = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    configuration_version = table.Column<int>(type: "int", nullable: false),
                    is_system_default = table.Column<bool>(type: "bit", nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dashboards", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recurrence_clusters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    auditable_entity_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    closed_exception_count = table.Column<int>(type: "int", nullable: false),
                    window_months = table.Column<int>(type: "int", nullable: false),
                    first_occurred_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    last_occurred_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    detected_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    notified_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    member_exception_ids_json = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurrence_clusters", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dashboard_widgets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    dashboard_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    widget_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    metric_key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    target_role_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    position = table.Column<int>(type: "int", nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    config_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dashboard_widgets", x => x.id);
                    table.ForeignKey(
                        name: "FK_dashboard_widgets_dashboards_dashboard_id",
                        column: x => x.dashboard_id,
                        principalTable: "dashboards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dashboard_widgets_dashboard_id_position",
                table: "dashboard_widgets",
                columns: new[] { "dashboard_id", "position" });

            migrationBuilder.CreateIndex(
                name: "IX_dashboards_slug",
                table: "dashboards",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recurrence_clusters_auditable_entity_id_category",
                table: "recurrence_clusters",
                columns: new[] { "auditable_entity_id", "category" },
                unique: true,
                filter: "[category] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dashboard_widgets");

            migrationBuilder.DropTable(
                name: "recurrence_clusters");

            migrationBuilder.DropTable(
                name: "dashboards");
        }
    }
}
