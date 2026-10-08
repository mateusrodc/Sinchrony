using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sinchrony.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClassSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsException",
                table: "classes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SeriesDate",
                table: "classes",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SeriesId",
                table: "classes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "class_series",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClassTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudioId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartTime = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Duration = table.Column<int>(type: "integer", nullable: false),
                    TotalSpots = table.Column<int>(type: "integer", nullable: false),
                    DaysOfWeekMask = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_class_series", x => x.Id);
                    table.ForeignKey(
                        name: "FK_class_series_class_types_ClassTypeId",
                        column: x => x.ClassTypeId,
                        principalTable: "class_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_series_studios_StudioId",
                        column: x => x.StudioId,
                        principalTable: "studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_series_users_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_classes_SeriesId_SeriesDate",
                table: "classes",
                columns: new[] { "SeriesId", "SeriesDate" },
                unique: true,
                filter: "\"SeriesId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_class_series_ClassTypeId",
                table: "class_series",
                column: "ClassTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_class_series_RequestId",
                table: "class_series",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_class_series_StudioId",
                table: "class_series",
                column: "StudioId");

            migrationBuilder.CreateIndex(
                name: "IX_class_series_TeacherId",
                table: "class_series",
                column: "TeacherId");

            migrationBuilder.AddForeignKey(
                name: "FK_classes_class_series_SeriesId",
                table: "classes",
                column: "SeriesId",
                principalTable: "class_series",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_classes_class_series_SeriesId",
                table: "classes");

            migrationBuilder.DropTable(
                name: "class_series");

            migrationBuilder.DropIndex(
                name: "IX_classes_SeriesId_SeriesDate",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "IsException",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "SeriesDate",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "classes");
        }
    }
}
