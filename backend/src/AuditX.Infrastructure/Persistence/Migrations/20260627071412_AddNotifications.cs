using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_dispatches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    event_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    event_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    rule_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    recipient_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    recipient_address = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    channel = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    template_key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    template_version = table.Column<int>(type: "int", nullable: false),
                    rendered_subject = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    rendered_body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    severity = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "int", nullable: false),
                    next_retry_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    dispatched_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    delivered_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    provider_message_id = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    provider_response_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    last_error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_dispatches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    event_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    recipient_resolution_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    channels_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    template_key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    is_system_default = table.Column<bool>(type: "bit", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    template_key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    channel = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    scope = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    subject_template = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    body_template = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    version = table.Column<int>(type: "int", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_templates", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notification_dispatches_event_id",
                table: "notification_dispatches",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "IX_notification_dispatches_event_id_rule_id_recipient_user_id_channel",
                table: "notification_dispatches",
                columns: new[] { "event_id", "rule_id", "recipient_user_id", "channel" },
                unique: true,
                filter: "[rule_id] IS NOT NULL AND [recipient_user_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notification_dispatches_status_next_retry_at",
                table: "notification_dispatches",
                columns: new[] { "status", "next_retry_at" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_rules_event_type_is_active",
                table: "notification_rules",
                columns: new[] { "event_type", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_templates_template_key_channel_scope",
                table: "notification_templates",
                columns: new[] { "template_key", "channel", "scope" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_dispatches");

            migrationBuilder.DropTable(
                name: "notification_rules");

            migrationBuilder.DropTable(
                name: "notification_templates");
        }
    }
}
