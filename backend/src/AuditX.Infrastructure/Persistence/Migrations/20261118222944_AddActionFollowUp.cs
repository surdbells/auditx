using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActionFollowUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_reminded_at",
                table: "map_actions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reminder_stage",
                table: "map_actions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "none");

            migrationBuilder.CreateTable(
                name: "due_date_extension_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    exception_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    action_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    current_date = table.Column<DateOnly>(type: "date", nullable: false),
                    requested_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    requested_by = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    decided_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    decision_note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_due_date_extension_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_due_date_extension_requests_exceptions_exception_id",
                        column: x => x.exception_id,
                        principalTable: "exceptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_map_actions_owner_user_id_status",
                table: "map_actions",
                columns: new[] { "owner_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_exceptions_raised_by_user_id",
                table: "exceptions",
                column: "raised_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_due_date_extension_requests_exception_id_status",
                table: "due_date_extension_requests",
                columns: new[] { "exception_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "due_date_extension_requests");

            migrationBuilder.DropIndex(
                name: "IX_map_actions_owner_user_id_status",
                table: "map_actions");

            migrationBuilder.DropIndex(
                name: "IX_exceptions_raised_by_user_id",
                table: "exceptions");

            migrationBuilder.DropColumn(
                name: "last_reminded_at",
                table: "map_actions");

            migrationBuilder.DropColumn(
                name: "reminder_stage",
                table: "map_actions");
        }
    }
}
