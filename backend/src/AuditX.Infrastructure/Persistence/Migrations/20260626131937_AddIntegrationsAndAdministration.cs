using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationsAndAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "max_audit_evidence_gb",
                table: "bank_settings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "max_evidence_file_mb",
                table: "bank_settings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "integration_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    connection_details_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    encrypted_credentials = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    timeout_seconds = table.Column<int>(type: "int", nullable: false),
                    fallback_integration_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    is_primary = table.Column<bool>(type: "bit", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_integration_configurations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "integration_health_status",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    integration_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    state = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    last_success_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    last_failure_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    recent_failure_count = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_health_status", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "object_restore_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    object_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    object_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    snapshot_date = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    justification = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    decision_comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_object_restore_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "release_installs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    version = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    manifest_sha256 = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    change_record_reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    detail = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_release_installs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "restore_drills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    executed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    outcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restore_drills", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "support_channel_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    engineer_identifiers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    enabled_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    enabled_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    revoked_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_channel_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "webhook_deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    event_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    event_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    payload_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    attempts = table.Column<int>(type: "int", nullable: false),
                    next_retry_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    last_error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    delivered_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_deliveries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "webhook_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    destination_url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    subscribed_event_types = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    encrypted_hmac_secret = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    retry_policy_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_webhook_subscriptions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_integration_configurations_is_active",
                table: "integration_configurations",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_integration_configurations_type",
                table: "integration_configurations",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "IX_integration_health_status_integration_id",
                table: "integration_health_status",
                column: "integration_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_support_channel_sessions_expires_at",
                table: "support_channel_sessions",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_status_next_retry_at",
                table: "webhook_deliveries",
                columns: new[] { "status", "next_retry_at" });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_subscription_id",
                table: "webhook_deliveries",
                column: "subscription_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integration_configurations");

            migrationBuilder.DropTable(
                name: "integration_health_status");

            migrationBuilder.DropTable(
                name: "object_restore_requests");

            migrationBuilder.DropTable(
                name: "release_installs");

            migrationBuilder.DropTable(
                name: "restore_drills");

            migrationBuilder.DropTable(
                name: "support_channel_sessions");

            migrationBuilder.DropTable(
                name: "webhook_deliveries");

            migrationBuilder.DropTable(
                name: "webhook_subscriptions");

            migrationBuilder.DropColumn(
                name: "max_audit_evidence_gb",
                table: "bank_settings");

            migrationBuilder.DropColumn(
                name: "max_evidence_file_mb",
                table: "bank_settings");
        }
    }
}
