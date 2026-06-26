using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_trail",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    actor_type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    actor_system_label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    originating_timezone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    event_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    target_object_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    target_object_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    before_state_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    after_state_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    request_context_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    event_payload_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_trail", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bank_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    bank_display_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    timezone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    locale_default = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ad_provisioning_filter_ou_dn = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ad_provisioning_filter_group_sid = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "maker_checker_actions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    action_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    target_object_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    target_object_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    maker_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    pending_payload_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    checker_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    resolution_comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maker_checker_actions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "maker_checker_gates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    action_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    is_enabled = table.Column<bool>(type: "bit", nullable: false),
                    checker_role_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    allow_maker_as_checker = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maker_checker_gates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    is_built_in = table.Column<bool>(type: "bit", nullable: false),
                    is_archived = table.Column<bool>(type: "bit", nullable: false),
                    parent_role_ids = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ad_sam_account_name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ad_user_principal_name = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ad_object_sid = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    first_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    last_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    timezone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    locale = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    notification_preferences_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    role_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    permission_key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    scope_type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    scope_predicate_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    role_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    scope_value = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    delegated_from_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    delegation_start = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    delegation_end = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_trail_actor_user_id",
                table: "audit_trail",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_trail_event_type",
                table: "audit_trail",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "IX_audit_trail_occurred_at_utc",
                table: "audit_trail",
                column: "occurred_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_audit_trail_target_object_type_target_object_id",
                table: "audit_trail",
                columns: new[] { "target_object_type", "target_object_id" });

            migrationBuilder.CreateIndex(
                name: "IX_maker_checker_actions_action_type_status",
                table: "maker_checker_actions",
                columns: new[] { "action_type", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_maker_checker_actions_status",
                table: "maker_checker_actions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_maker_checker_gates_action_type",
                table: "maker_checker_gates",
                column: "action_type",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_role_id",
                table: "role_permissions",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_role_id_permission_key",
                table: "role_permissions",
                columns: new[] { "role_id", "permission_key" });

            migrationBuilder.CreateIndex(
                name: "IX_roles_name",
                table: "roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_delegated_from_user_id_is_active",
                table: "user_roles",
                columns: new[] { "delegated_from_user_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_role_id",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_user_id",
                table: "user_roles",
                column: "user_id");

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
                name: "IX_users_status",
                table: "users",
                column: "status");

            // M11: enforce the append-only audit trail at the database level. The trigger rejects any
            // UPDATE or DELETE against audit_trail, so tampering is impossible even with direct SQL access.
            migrationBuilder.Sql(
                """
                CREATE TRIGGER tr_audit_trail_append_only
                ON audit_trail
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    THROW 53001, 'The audit trail is append-only; UPDATE and DELETE are not permitted.', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_audit_trail_append_only;");

            migrationBuilder.DropTable(
                name: "audit_trail");

            migrationBuilder.DropTable(
                name: "bank_settings");

            migrationBuilder.DropTable(
                name: "maker_checker_actions");

            migrationBuilder.DropTable(
                name: "maker_checker_gates");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
