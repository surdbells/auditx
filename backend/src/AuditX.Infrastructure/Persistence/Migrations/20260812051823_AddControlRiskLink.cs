using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddControlRiskLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "control_risk_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    control_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    risk_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    linked_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_control_risk_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_control_risk_links_controls_control_id",
                        column: x => x.control_id,
                        principalTable: "controls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_control_risk_links_risks_risk_id",
                        column: x => x.risk_id,
                        principalTable: "risks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_control_risk_links_control_id_risk_id",
                table: "control_risk_links",
                columns: new[] { "control_id", "risk_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_control_risk_links_risk_id",
                table: "control_risk_links",
                column: "risk_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "control_risk_links");
        }
    }
}
