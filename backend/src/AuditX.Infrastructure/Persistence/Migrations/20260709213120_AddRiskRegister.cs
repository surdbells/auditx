using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRiskRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "risks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    auditable_entity_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    inherent_likelihood = table.Column<int>(type: "int", nullable: false),
                    inherent_impact = table.Column<int>(type: "int", nullable: false),
                    residual_likelihood = table.Column<int>(type: "int", nullable: true),
                    residual_impact = table.Column<int>(type: "int", nullable: true),
                    treatment_strategy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    treatment_plan = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    target_date = table.Column<DateOnly>(type: "date", nullable: true),
                    next_review_date = table.Column<DateOnly>(type: "date", nullable: true),
                    identified_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    identified_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    closure_rationale = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    closed_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risks", x => x.id);
                    table.ForeignKey(
                        name: "FK_risks_audit_universe_entities_auditable_entity_id",
                        column: x => x.auditable_entity_id,
                        principalTable: "audit_universe_entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_risks_auditable_entity_id",
                table: "risks",
                column: "auditable_entity_id");

            migrationBuilder.CreateIndex(
                name: "IX_risks_category",
                table: "risks",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_risks_owner_user_id",
                table: "risks",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_risks_status",
                table: "risks",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "risks");
        }
    }
}
