using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportChannelAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "support_access_expires_at",
                table: "users",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "support_channel_session_id",
                table: "users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_support_access_expires_at",
                table: "users",
                column: "support_access_expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_users_support_channel_session_id",
                table: "users",
                column: "support_channel_session_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_support_access_expires_at",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_support_channel_session_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "support_access_expires_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "support_channel_session_id",
                table: "users");
        }
    }
}
