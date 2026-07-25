using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sinchrony.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageUnitId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UnitId",
                table: "packages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_packages_UnitId",
                table: "packages",
                column: "UnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_packages_units_UnitId",
                table: "packages",
                column: "UnitId",
                principalTable: "units",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_packages_units_UnitId",
                table: "packages");

            migrationBuilder.DropIndex(
                name: "IX_packages_UnitId",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "UnitId",
                table: "packages");
        }
    }
}
