using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResponseOptionSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "selected_option_code",
                table: "checklist_responses",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "selected_option_label",
                table: "checklist_responses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "response_option_sets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    response_type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    options_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_response_option_sets", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_response_option_sets_response_type",
                table: "response_option_sets",
                column: "response_type",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "response_option_sets");

            migrationBuilder.DropColumn(
                name: "selected_option_code",
                table: "checklist_responses");

            migrationBuilder.DropColumn(
                name: "selected_option_label",
                table: "checklist_responses");
        }
    }
}
