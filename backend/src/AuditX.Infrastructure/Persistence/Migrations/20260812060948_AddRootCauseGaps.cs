using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRootCauseGaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "root_cause_gaps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    owner_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    target_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    identified_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    identified_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    closure_rationale = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    closed_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_root_cause_gaps", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "root_cause_gap_exception_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    root_cause_gap_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    exception_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    linked_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_root_cause_gap_exception_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_root_cause_gap_exception_links_exceptions_exception_id",
                        column: x => x.exception_id,
                        principalTable: "exceptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_root_cause_gap_exception_links_root_cause_gaps_root_cause_gap_id",
                        column: x => x.root_cause_gap_id,
                        principalTable: "root_cause_gaps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_root_cause_gap_exception_links_exception_id",
                table: "root_cause_gap_exception_links",
                column: "exception_id");

            migrationBuilder.CreateIndex(
                name: "IX_root_cause_gap_exception_links_root_cause_gap_id_exception_id",
                table: "root_cause_gap_exception_links",
                columns: new[] { "root_cause_gap_id", "exception_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_root_cause_gaps_status",
                table: "root_cause_gaps",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "root_cause_gap_exception_links");

            migrationBuilder.DropTable(
                name: "root_cause_gaps");
        }
    }
}
