using Sinchrony.Application.Common;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Attendance;

public record AttendanceChangeItem(Booking Booking, string Status);

public record AttendanceChangeResult(int Created, int Updated, IReadOnlyList<string> Warnings);

// Ponto único de mudança de presença/falta (DEMANDA_PRESENCA_EM_AULA_FUTURA_BACKEND.md). Usado pelos
// 5 caminhos que lançam chamada (PUT/bulk/confirm-all em /classes, POST /api/checkin/{id}/confirm e
// PATCH /api/bookings/{id}/no-show) pra guarda de horário, auditoria e sincronia
// AttendanceRecord <-> Booking não divergirem de novo.
public class AttendanceChangeService(
    IAttendanceRepository attendanceRepository,
    IBookingRepository bookingRepository,
    ISettingsRepository settingsRepository,
    INoShowPenaltyService noShowPenaltyService,
    IWaitlistPromotionService waitlistPromotionService,
    IAuditService auditService)
{
    // Presença abre 30 min antes do início da aula (régua confirmada pelo Raian em 02/10/2026).
    public const int OpenMinutesBeforeStart = 30;

    public const string SourceSingle = "single";
    public const string SourceBulk = "bulk";
    public const string SourceConfirmAll = "confirm_all";
    public const string SourceErpCheckin = "erp_checkin";
    public const string SourceErpNoShow = "erp_no_show";

    public const string WaitlistNotUndoneWarning =
        "A falta foi desfeita, mas a vaga já liberada para a lista de espera não é desfeita automaticamente.";
    public const string CreditNotRevertedWarning =
        "A falta foi desfeita, mas não foi possível retirar o crédito devolvido (o aluno não tem saldo suficiente).";

    /// <summary>Normaliza o status recebido: "pending" e "confirmed" voltam o registro pra pendente.</summary>
    public static string Normalize(string? status) => status switch
    {
        "attended" => "attended",
        "no_show" => "no_show",
        "pending" or "confirmed" => "confirmed",
        _ => throw DomainException.Validation("INVALID_STATUS",
            "Status inválido. Use attended, no_show ou pending.")
    };

    /// <summary>
    /// Regra de horário. Devolve null se o status é aceito agora, ou (código, mensagem) se recusado.
    /// Class.Date/StartTime são horário de Brasília, por isso a comparação é contra BrasiliaTime.
    /// </summary>
    public static (string Code, string Message)? CheckWindow(
        Class @class, string normalizedStatus, DateTime nowUtc, int toleranceMinutes)
    {
        if (normalizedStatus == "confirmed")
            return null; // desfazer lançamento errado é sempre aceito

        if (@class.Status == ClassStatus.cancelled)
            return ("CLASS_CANCELLED", "Esta aula foi cancelada.");

        if (!TimeOnly.TryParse(@class.StartTime, out var start))
            throw DomainException.Validation("INVALID_CLASS_TIME", "A aula não tem um horário de início válido.");

        var classStart = @class.Date.ToDateTime(start);
        var nowLocal = BrasiliaTime.Convert(nowUtc);

        if (normalizedStatus == "attended")
        {
            return nowLocal < classStart.AddMinutes(-OpenMinutesBeforeStart)
                ? ("CLASS_NOT_STARTED",
                    $"Esta aula ainda não começou. A chamada abre {OpenMinutesBeforeStart} minutos antes do início.")
                : null;
        }

        // no_show: mesma régua do ToleranceEnforcementService (só depois de início + tolerância).
        return nowLocal <= classStart.AddMinutes(toleranceMinutes)
            ? ("NO_SHOW_TOO_EARLY",
                "A falta só pode ser lançada depois do início da aula e do tempo de tolerância.")
            : null;
    }

    /// <summary>Recusa (422 + auditoria attendance.rejected) se algum dos status não pode ser lançado agora.</summary>
    public async Task EnsureAllowedAsync(
        Class @class, IEnumerable<string> normalizedStatuses, Guid? userId, string source, CancellationToken ct)
    {
        var settings = await settingsRepository.GetAsync(ct);
        var tolerance = settings?.ToleranceMinutes ?? 10;
        var now = DateTime.UtcNow;

        foreach (var status in normalizedStatuses.Distinct())
        {
            var rejection = CheckWindow(@class, status, now, tolerance);
            if (rejection is null) continue;

            await auditService.LogAsync(
                "attendance.rejected", "Class", @class.Id, userId,
                $"ClassId: {@class.Id}, Status: {status}, Code: {rejection.Value.Code}, Source: {source}",
                ct: ct);

            throw DomainException.Validation(rejection.Value.Code, rejection.Value.Message);
        }
    }

    /// <summary>
    /// Valida o horário de todos os itens (recusa o pedido inteiro) e só então aplica, atualizando
    /// AttendanceRecord e Booking juntos, auditando e disparando penalidade/fila.
    /// </summary>
    public async Task<AttendanceChangeResult> ApplyAsync(
        Class @class, IReadOnlyList<AttendanceChangeItem> items, Guid? userId, string source, CancellationToken ct)
    {
        var normalized = items.Select(i => Normalize(i.Status)).ToList();
        await EnsureAllowedAsync(@class, normalized, userId, source, ct);

        var created = 0;
        var updated = 0;
        var warnings = new List<string>();
        var newlyNoShow = new List<Guid>();
        var undoneNoShow = new List<Guid>();
        var audits = new List<(AttendanceRecord Record, Guid StudentId, string Previous, string New)>();

        for (var i = 0; i < items.Count; i++)
        {
            var booking = items[i].Booking;
            var status = normalized[i];

            var attendance = await attendanceRepository.GetByBookingAsync(booking.Id, ct);
            var isNew = attendance is null;
            if (isNew)
            {
                attendance = AttendanceRecord.Create(booking.Id, @class.Id, booking.StudentId);
                await attendanceRepository.AddAsync(attendance, ct);
                created++;
            }
            else
            {
                updated++;
            }

            var target = status switch
            {
                "attended" => BookingStatus.attended,
                "no_show" => BookingStatus.no_show,
                _ => BookingStatus.confirmed
            };

            // Sem registro de presença, o estado anterior é o da reserva (ex: no_show do ERP antigo).
            var previous = isNew ? booking.Status : attendance!.Status;
            var wasNoShow = previous == BookingStatus.no_show || booking.Status == BookingStatus.no_show;

            if (!isNew && attendance!.Status == target && booking.Status == target)
                continue; // nada mudou: sem escrita, sem auditoria

            attendance!.UpdateStatus(status, userId);
            switch (target)
            {
                case BookingStatus.attended: booking.MarkAttended(); break;
                case BookingStatus.no_show: booking.MarkNoShow(); booking.SetCheckedIn(false); break;
                default: booking.MarkConfirmed(); break;
            }

            audits.Add((attendance, booking.StudentId, previous.ToString(), target.ToString()));

            if (target == BookingStatus.no_show && !wasNoShow)
                newlyNoShow.Add(booking.StudentId);
            else if (target != BookingStatus.no_show && wasNoShow)
                undoneNoShow.Add(booking.StudentId);
        }

        await attendanceRepository.SaveAsync(ct);
        await bookingRepository.SaveAsync(ct);

        foreach (var (record, studentId, previous, now) in audits)
        {
            await auditService.LogAsync(
                "attendance.updated", "AttendanceRecord", record.Id, userId,
                $"ClassId: {@class.Id}, StudentId: {studentId}, Previous: {previous}, New: {now}, Source: {source}",
                ct: ct);
        }

        foreach (var studentId in newlyNoShow)
        {
            await noShowPenaltyService.ApplyAsync(studentId, ct);
            // Sempre uma aula só — mesma @class pra todo mundo.
            await waitlistPromotionService.PromoteNextAsync(@class.Id, @class.Name, ct);
        }

        foreach (var studentId in undoneNoShow)
        {
            if (!await noShowPenaltyService.ReverseAsync(studentId, ct)
                && !warnings.Contains(CreditNotRevertedWarning))
                warnings.Add(CreditNotRevertedWarning);
        }
        if (undoneNoShow.Count > 0)
            warnings.Add(WaitlistNotUndoneWarning);

        return new AttendanceChangeResult(created, updated, warnings);
    }
}
