using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSanctions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sanctions_cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    exception_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    subject_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    is_recurrence = table.Column<bool>(type: "bit", nullable: false),
                    recommendation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    grid_consulted_version = table.Column<int>(type: "int", nullable: true),
                    grid_recommended_range = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    within_grid_range = table.Column<bool>(type: "bit", nullable: false),
                    deviation_reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    hr_outcome_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    dc_decision_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    triggered_by = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    triggered_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    recommended_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    recommended_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    hr_outcome_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    dc_decision_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    closed_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sanctions_cases", x => x.id);
                    table.ForeignKey(
                        name: "FK_sanctions_cases_exceptions_exception_id",
                        column: x => x.exception_id,
                        principalTable: "exceptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sanctions_grid_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    version_number = table.Column<int>(type: "int", nullable: false),
                    grid_definition_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    activation_reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    activated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    activated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sanctions_grid_versions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sanctions_appeals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sanctions_case_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    appellant_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    routed_to_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    basis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    decision_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    filed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sanctions_appeals", x => x.id);
                    table.ForeignKey(
                        name: "FK_sanctions_appeals_sanctions_cases_sanctions_case_id",
                        column: x => x.sanctions_case_id,
                        principalTable: "sanctions_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sanctions_case_team",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sanctions_case_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    role_marker = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sanctions_case_team", x => x.id);
                    table.ForeignKey(
                        name: "FK_sanctions_case_team_sanctions_cases_sanctions_case_id",
                        column: x => x.sanctions_case_id,
                        principalTable: "sanctions_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sanctions_appeals_routed_to_user_id_status",
                table: "sanctions_appeals",
                columns: new[] { "routed_to_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_sanctions_appeals_sanctions_case_id",
                table: "sanctions_appeals",
                column: "sanctions_case_id");

            migrationBuilder.CreateIndex(
                name: "IX_sanctions_case_team_sanctions_case_id",
                table: "sanctions_case_team",
                column: "sanctions_case_id");

            migrationBuilder.CreateIndex(
                name: "IX_sanctions_case_team_sanctions_case_id_user_id",
                table: "sanctions_case_team",
                columns: new[] { "sanctions_case_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sanctions_cases_exception_id",
                table: "sanctions_cases",
                column: "exception_id");

            migrationBuilder.CreateIndex(
                name: "IX_sanctions_cases_status",
                table: "sanctions_cases",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_sanctions_cases_subject_user_id",
                table: "sanctions_cases",
                column: "subject_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_sanctions_grid_versions_is_active",
                table: "sanctions_grid_versions",
                column: "is_active",
                unique: true,
                filter: "[is_active] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_sanctions_grid_versions_version_number",
                table: "sanctions_grid_versions",
                column: "version_number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sanctions_appeals");

            migrationBuilder.DropTable(
                name: "sanctions_case_team");

            migrationBuilder.DropTable(
                name: "sanctions_grid_versions");

            migrationBuilder.DropTable(
                name: "sanctions_cases");
        }
    }
}
