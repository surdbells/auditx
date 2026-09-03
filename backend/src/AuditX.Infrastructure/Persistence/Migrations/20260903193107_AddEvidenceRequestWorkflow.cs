using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidenceRequestWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "exception_id",
                table: "evidence_requests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "purpose",
                table: "evidence_requests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "review_document");

            migrationBuilder.AddColumn<Guid>(
                name: "requested_from_user_id",
                table: "evidence_requests",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_evidence_requests_requested_from_user_id",
                table: "evidence_requests",
                column: "requested_from_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_evidence_requests_requested_from_user_id",
                table: "evidence_requests");

            migrationBuilder.DropColumn(
                name: "exception_id",
                table: "evidence_requests");

            migrationBuilder.DropColumn(
                name: "purpose",
                table: "evidence_requests");

            migrationBuilder.DropColumn(
                name: "requested_from_user_id",
                table: "evidence_requests");
        }
    }
}
