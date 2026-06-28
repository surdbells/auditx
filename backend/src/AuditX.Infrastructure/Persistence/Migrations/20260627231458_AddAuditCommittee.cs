using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditCommittee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ac_action_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    assigned_to_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    closure_response = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    closed_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    acknowledged_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    acknowledged_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    deletion_reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ac_action_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ac_comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    target_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    target_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    comment_text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    commented_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ac_comments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ac_packs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    version_number = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    ac_meeting_label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    content_snapshot_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    cia_supplementary_text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    artefact_storage_path = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    sha256_hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    produced_artefacts_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    requested_formats_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    failure_reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    generated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    approved_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    deletion_reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ac_packs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "finding_visibility_restrictions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    finding_type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    finding_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    allowed_user_ids_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    restricted_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    restricted_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finding_visibility_restrictions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ac_pack_distributions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ac_pack_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ac_pack_version_number = table.Column<int>(type: "int", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    dispatched_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    dispatched_by = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ac_pack_distributions", x => x.id);
                    table.ForeignKey(
                        name: "FK_ac_pack_distributions_ac_packs_ac_pack_id",
                        column: x => x.ac_pack_id,
                        principalTable: "ac_packs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ac_action_items_status",
                table: "ac_action_items",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_ac_comments_target_type_target_id",
                table: "ac_comments",
                columns: new[] { "target_type", "target_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ac_pack_distributions_ac_pack_id",
                table: "ac_pack_distributions",
                column: "ac_pack_id");

            migrationBuilder.CreateIndex(
                name: "IX_ac_pack_distributions_ac_pack_id_dispatched_at",
                table: "ac_pack_distributions",
                columns: new[] { "ac_pack_id", "dispatched_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ac_packs_status",
                table: "ac_packs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_ac_packs_version_number",
                table: "ac_packs",
                column: "version_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finding_visibility_restrictions_finding_type_finding_id",
                table: "finding_visibility_restrictions",
                columns: new[] { "finding_type", "finding_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ac_action_items");

            migrationBuilder.DropTable(
                name: "ac_comments");

            migrationBuilder.DropTable(
                name: "ac_pack_distributions");

            migrationBuilder.DropTable(
                name: "finding_visibility_restrictions");

            migrationBuilder.DropTable(
                name: "ac_packs");
        }
    }
}
