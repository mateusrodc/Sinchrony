using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sinchrony.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWaitlistAndTermsAndSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ToleranceMinutes",
                table: "settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ToleranceMode",
                table: "settings",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ToleranceMinutes",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "ToleranceMode",
                table: "settings");
        }
    }
}
