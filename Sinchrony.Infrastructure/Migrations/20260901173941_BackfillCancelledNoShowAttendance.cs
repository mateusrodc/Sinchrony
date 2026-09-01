using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sinchrony.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillCancelledNoShowAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill do bug corrigido em CancelBookingCommand.cs (commit 1c68c13, 2026-08-11):
            // antes da correção, TODO cancelamento feito pelo aluno via App gravava o
            // AttendanceRecord como "no_show", mesmo quando o cancelamento estava dentro do
            // prazo (a checagem de CancellationDeadlineHours já tinha passado antes dessa
            // linha rodar — ou seja, todo registro afetado representa, por construção, um
            // cancelamento legítimo dentro do prazo, não uma falta real).
            //
            // A combinação Booking.Status = 'cancelled' + AttendanceRecord.Status = 'no_show'
            // é a assinatura exclusiva desse bug: não existe nenhum outro caminho no código
            // (histórico ou atual) que produza esse par de valores — cancelamento via ERP
            // nunca tocou o AttendanceRecord, e falta real (manual ou por tolerância) sempre
            // deixa o Booking em 'no_show', não 'cancelled'. Por isso é seguro corrigir em
            // massa sem precisar cruzar data/hora do cancelamento contra o prazo do pacote.
            migrationBuilder.Sql("""
                UPDATE attendance_records ar
                SET "Status" = 'cancelled'
                FROM bookings b
                WHERE ar."BookingId" = b."Id"
                  AND ar."Status" = 'no_show'
                  AND b."Status" = 'cancelled';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Não reversível de propósito: não há como distinguir, depois do Up, quais
            // registros eram "no_show" corrigidos por esta migration vs. já eram "cancelled"
            // por outro motivo. Reverter voltaria a introduzir o dado incorreto que este
            // backfill existe justamente para corrigir.
        }
    }
}
