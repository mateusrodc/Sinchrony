using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sinchrony.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDependentUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_dependents_UserId",
                table: "dependents",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_dependents_users_UserId",
                table: "dependents",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dependents_users_UserId",
                table: "dependents");

            migrationBuilder.DropIndex(
                name: "IX_dependents_UserId",
                table: "dependents");
        }
    }
}
