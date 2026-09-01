using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sinchrony.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentMethodsAndClassTypeFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowsCard",
                table: "packages",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowsInstallments",
                table: "packages",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowsPix",
                table: "packages",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxInstallments",
                table: "packages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UsesJump",
                table: "class_types",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UsesPilatesMat",
                table: "class_types",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowsCard",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "AllowsInstallments",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "AllowsPix",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "MaxInstallments",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "UsesJump",
                table: "class_types");

            migrationBuilder.DropColumn(
                name: "UsesPilatesMat",
                table: "class_types");
        }
    }
}
