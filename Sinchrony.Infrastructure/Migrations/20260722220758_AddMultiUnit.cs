using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sinchrony.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsGlobalAdmin",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "UnitId",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UnitId",
                table: "studios",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "units",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_UnitId",
                table: "users",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_studios_UnitId",
                table: "studios",
                column: "UnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_studios_units_UnitId",
                table: "studios",
                column: "UnitId",
                principalTable: "units",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_users_units_UnitId",
                table: "users",
                column: "UnitId",
                principalTable: "units",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_studios_units_UnitId",
                table: "studios");

            migrationBuilder.DropForeignKey(
                name: "FK_users_units_UnitId",
                table: "users");

            migrationBuilder.DropTable(
                name: "units");

            migrationBuilder.DropIndex(
                name: "IX_users_UnitId",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_studios_UnitId",
                table: "studios");

            migrationBuilder.DropColumn(
                name: "IsGlobalAdmin",
                table: "users");

            migrationBuilder.DropColumn(
                name: "UnitId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "UnitId",
                table: "studios");
        }
    }
}
