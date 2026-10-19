using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sinchrony.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncAttendanceWithBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Registros de presença que ficaram para trás na remarcação: voltam para a aula da reserva.
            migrationBuilder.Sql("""
                UPDATE attendance_records a
                SET "ClassId" = b."ClassId"
                FROM bookings b
                WHERE a."BookingId" = b."Id" AND a."ClassId" <> b."ClassId";
                """);

            // Reservas canceladas pela equipe cujo registro continuou ativo.
            migrationBuilder.Sql("""
                UPDATE attendance_records a
                SET "Status" = 'cancelled'
                FROM bookings b
                WHERE a."BookingId" = b."Id" AND b."Status" = 'cancelled' AND a."Status" <> 'cancelled';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
