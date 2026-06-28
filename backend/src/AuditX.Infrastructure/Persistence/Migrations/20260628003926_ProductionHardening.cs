using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductionHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_ad_object_sid",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_ad_sam_account_name",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_ad_user_principal_name",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_templates_name_audit_type",
                table: "templates");

            migrationBuilder.DropIndex(
                name: "IX_report_templates_is_active",
                table: "report_templates");

            migrationBuilder.DropIndex(
                name: "IX_dashboards_slug",
                table: "dashboards");

            migrationBuilder.DropIndex(
                name: "IX_bank_configurations_domain_is_active",
                table: "bank_configurations");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_status_created_at",
                table: "webhook_deliveries",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_users_ad_object_sid",
                table: "users",
                column: "ad_object_sid",
                unique: true,
                filter: "[is_deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_users_ad_sam_account_name",
                table: "users",
                column: "ad_sam_account_name",
                unique: true,
                filter: "[is_deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_users_ad_user_principal_name",
                table: "users",
                column: "ad_user_principal_name",
                unique: true,
                filter: "[is_deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_templates_name_audit_type",
                table: "templates",
                columns: new[] { "name", "audit_type" },
                unique: true,
                filter: "[is_deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_report_templates_is_active",
                table: "report_templates",
                column: "is_active",
                unique: true,
                filter: "[is_active] = 1 AND [is_deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_notification_dispatches_event_type_id",
                table: "notification_dispatches",
                columns: new[] { "event_type", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_dispatches_recipient_user_id_id",
                table: "notification_dispatches",
                columns: new[] { "recipient_user_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_dashboards_slug",
                table: "dashboards",
                column: "slug",
                unique: true,
                filter: "[is_deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_bank_configurations_domain_is_active",
                table: "bank_configurations",
                columns: new[] { "domain", "is_active" },
                unique: true,
                filter: "[is_active] = 1 AND [is_deleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_webhook_deliveries_status_created_at",
                table: "webhook_deliveries");

            migrationBuilder.DropIndex(
                name: "IX_users_ad_object_sid",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_ad_sam_account_name",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_ad_user_principal_name",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_templates_name_audit_type",
                table: "templates");

            migrationBuilder.DropIndex(
                name: "IX_report_templates_is_active",
                table: "report_templates");

            migrationBuilder.DropIndex(
                name: "IX_notification_dispatches_event_type_id",
                table: "notification_dispatches");

            migrationBuilder.DropIndex(
                name: "IX_notification_dispatches_recipient_user_id_id",
                table: "notification_dispatches");

            migrationBuilder.DropIndex(
                name: "IX_dashboards_slug",
                table: "dashboards");

            migrationBuilder.DropIndex(
                name: "IX_bank_configurations_domain_is_active",
                table: "bank_configurations");

            migrationBuilder.CreateIndex(
                name: "IX_users_ad_object_sid",
                table: "users",
                column: "ad_object_sid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_ad_sam_account_name",
                table: "users",
                column: "ad_sam_account_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_ad_user_principal_name",
                table: "users",
                column: "ad_user_principal_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_templates_name_audit_type",
                table: "templates",
                columns: new[] { "name", "audit_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_report_templates_is_active",
                table: "report_templates",
                column: "is_active",
                unique: true,
                filter: "[is_active] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_dashboards_slug",
                table: "dashboards",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bank_configurations_domain_is_active",
                table: "bank_configurations",
                columns: new[] { "domain", "is_active" },
                unique: true,
                filter: "[is_active] = 1");
        }
    }
}
