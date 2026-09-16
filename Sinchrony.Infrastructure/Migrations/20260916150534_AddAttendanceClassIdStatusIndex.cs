using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sinchrony.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceClassIdStatusIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_attendance_records_ClassId",
                table: "attendance_records");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_records_ClassId_Status",
                table: "attendance_records",
                columns: new[] { "ClassId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_attendance_records_ClassId_Status",
                table: "attendance_records");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_records_ClassId",
                table: "attendance_records",
                column: "ClassId");
        }
    }
}
