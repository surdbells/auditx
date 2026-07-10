using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFindingFollowUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "management_responded_at",
                table: "exceptions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "management_responded_by",
                table: "exceptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "management_response_comment",
                table: "exceptions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "management_response_decision",
                table: "exceptions",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "reopen_count",
                table: "exceptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "reopen_reason",
                table: "exceptions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reopened_at",
                table: "exceptions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reopened_by",
                table: "exceptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "finding_verifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    exception_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    result = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    verified_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    verified_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finding_verifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_finding_verifications_exceptions_exception_id",
                        column: x => x.exception_id,
                        principalTable: "exceptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_finding_verifications_exception_id",
                table: "finding_verifications",
                column: "exception_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "finding_verifications");

            migrationBuilder.DropColumn(
                name: "management_responded_at",
                table: "exceptions");

            migrationBuilder.DropColumn(
                name: "management_responded_by",
                table: "exceptions");

            migrationBuilder.DropColumn(
                name: "management_response_comment",
                table: "exceptions");

            migrationBuilder.DropColumn(
                name: "management_response_decision",
                table: "exceptions");

            migrationBuilder.DropColumn(
                name: "reopen_count",
                table: "exceptions");

            migrationBuilder.DropColumn(
                name: "reopen_reason",
                table: "exceptions");

            migrationBuilder.DropColumn(
                name: "reopened_at",
                table: "exceptions");

            migrationBuilder.DropColumn(
                name: "reopened_by",
                table: "exceptions");
        }
    }
}
