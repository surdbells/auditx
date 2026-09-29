using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Renames the "bank" nomenclature to institution-neutral names so the platform can serve any
    /// institution (banks, FMCG, corporates, audit firms, …). This is a data-preserving rename:
    /// tables, the display-name column, indexes and primary keys are renamed in place (no drop/create),
    /// and stored values that encode the old naming (settings permission keys and the notification
    /// template scope) are updated. Append-only <c>audit_trail</c> rows are intentionally left untouched
    /// (the table is trigger-protected against UPDATE); new rows use the new event/target constants.
    /// </summary>
    public partial class RenameBankToInstitution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- Tables ----
            migrationBuilder.RenameTable(name: "bank_settings", newName: "institution_settings");
            migrationBuilder.RenameTable(name: "bank_configurations", newName: "institution_configurations");

            // ---- Columns ----
            migrationBuilder.RenameColumn(
                name: "bank_display_name",
                table: "institution_settings",
                newName: "institution_display_name");

            // ---- Indexes ----
            migrationBuilder.RenameIndex(
                name: "IX_bank_configurations_domain_is_active",
                table: "institution_configurations",
                newName: "IX_institution_configurations_domain_is_active");
            migrationBuilder.RenameIndex(
                name: "IX_bank_configurations_domain_version_number",
                table: "institution_configurations",
                newName: "IX_institution_configurations_domain_version_number");

            // ---- Primary keys (rename the constraint objects to match the new table names) ----
            migrationBuilder.Sql("EXEC sp_rename N'PK_bank_settings', N'PK_institution_settings';");
            migrationBuilder.Sql("EXEC sp_rename N'PK_bank_configurations', N'PK_institution_configurations';");

            // ---- Widen the notification template scope column (system|institution — 11 chars) ----
            migrationBuilder.AlterColumn<string>(
                name: "scope",
                table: "notification_templates",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            // ---- Stored values that encode the old naming ----
            migrationBuilder.Sql(
                "UPDATE role_permissions SET permission_key = 'ViewInstitutionSettings' WHERE permission_key = 'ViewBankSettings';");
            migrationBuilder.Sql(
                "UPDATE role_permissions SET permission_key = 'ManageInstitutionSettings' WHERE permission_key = 'ManageBankSettings';");
            migrationBuilder.Sql(
                "UPDATE notification_templates SET scope = 'institution' WHERE scope = 'bank';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ---- Revert stored values ----
            migrationBuilder.Sql(
                "UPDATE notification_templates SET scope = 'bank' WHERE scope = 'institution';");
            migrationBuilder.Sql(
                "UPDATE role_permissions SET permission_key = 'ManageBankSettings' WHERE permission_key = 'ManageInstitutionSettings';");
            migrationBuilder.Sql(
                "UPDATE role_permissions SET permission_key = 'ViewBankSettings' WHERE permission_key = 'ViewInstitutionSettings';");

            migrationBuilder.AlterColumn<string>(
                name: "scope",
                table: "notification_templates",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            // ---- Primary keys ----
            migrationBuilder.Sql("EXEC sp_rename N'PK_institution_configurations', N'PK_bank_configurations';");
            migrationBuilder.Sql("EXEC sp_rename N'PK_institution_settings', N'PK_bank_settings';");

            // ---- Indexes (tables are still institution_* here; renamed back last) ----
            migrationBuilder.RenameIndex(
                name: "IX_institution_configurations_domain_version_number",
                table: "institution_configurations",
                newName: "IX_bank_configurations_domain_version_number");
            migrationBuilder.RenameIndex(
                name: "IX_institution_configurations_domain_is_active",
                table: "institution_configurations",
                newName: "IX_bank_configurations_domain_is_active");

            // ---- Columns (table is still institution_settings here) ----
            migrationBuilder.RenameColumn(
                name: "institution_display_name",
                table: "institution_settings",
                newName: "bank_display_name");

            // ---- Tables ----
            migrationBuilder.RenameTable(name: "institution_configurations", newName: "bank_configurations");
            migrationBuilder.RenameTable(name: "institution_settings", newName: "bank_settings");
        }
    }
}
