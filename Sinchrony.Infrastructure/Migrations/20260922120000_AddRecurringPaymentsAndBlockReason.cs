using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sinchrony.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringPaymentsAndBlockReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRecurring",
                table: "packages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AsaasSubscriptionId",
                table: "student_packages",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlockedReason",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_packages_AsaasSubscriptionId",
                table: "student_packages",
                column: "AsaasSubscriptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_student_packages_AsaasSubscriptionId",
                table: "student_packages");

            migrationBuilder.DropColumn(
                name: "IsRecurring",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "AsaasSubscriptionId",
                table: "student_packages");

            migrationBuilder.DropColumn(
                name: "BlockedReason",
                table: "users");
        }
    }
}
