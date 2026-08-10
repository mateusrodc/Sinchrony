using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sinchrony.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentPackageSourceAndCreditsGranted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreditsGranted",
                table: "student_packages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "student_packages",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "purchase");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreditsGranted",
                table: "student_packages");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "student_packages");
        }
    }
}
