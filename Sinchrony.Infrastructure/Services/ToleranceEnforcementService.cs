using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Infrastructure.Persistence;

namespace Sinchrony.Infrastructure.Services;

// Cláusula 10.2 do Termo, modo "automatic" (DEMANDA_LISTA_ESPERA_TOLERANCIA_NOSHOW_PACOTES_BACKEND.md,
// item 2). Não existe nenhuma outra infraestrutura de job no projeto (sem Hangfire/Quartz) — usei
// BackgroundService puro do próprio .NET pra não introduzir uma dependência nova sem poder validar
// que ela builda neste ambiente. Corre a cada 1 minuto; não faz nada se Settings.ToleranceMode não
// for "automatic" (modo "manual", o padrão, continua exatamente como já era: equipe marca na tela
// de check-in, sem ação automática).
public class ToleranceEnforcementService(
    IServiceScopeFactory scopeFactory,
    ILogger<ToleranceEnforcementService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // Uma falha nesse tick não pode derrubar o loop — tenta de novo no próximo minuto.
                logger.LogError(ex, "ToleranceEnforcementService: falha ao processar tolerância automática.");
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var waitlistPromotionService = scope.ServiceProvider.GetRequiredService<IWaitlistPromotionService>();
        var noShowPenaltyService = scope.ServiceProvider.GetRequiredService<INoShowPenaltyService>();
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

        var settings = await db.Settings.FirstOrDefaultAsync(ct);
        if (settings is null || settings.ToleranceMode != "automatic")
            return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);

        // Filtro amplo traduzível pro SQL primeiro (evita full scan); o cálculo exato de
        // ClassStart usa TimeOnly.Parse, que não é traduzível — feito em memória depois.
        var candidates = await db.Bookings
            .Include(b => b.Class)
            .Where(b => b.Status == BookingStatus.confirmed && !b.CheckedIn &&
                        b.Class != null && b.Class.Date >= yesterday && b.Class.Date <= today)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var overdue = candidates.Where(b =>
        {
            if (b.Class is null || !TimeOnly.TryParse(b.Class.StartTime, out var start))
                return false;
            var classStart = b.Class.Date.ToDateTime(start);
            return now > classStart.AddMinutes(settings.ToleranceMinutes);
        }).ToList();

        if (overdue.Count == 0)
            return;

        foreach (var booking in overdue)
        {
            booking.MarkNoShow();

            var attendance = await db.AttendanceRecords
                .FirstOrDefaultAsync(a => a.BookingId == booking.Id, ct);
            attendance?.MarkNoShow();

            // Devolve o crédito se o pacote do aluno tiver NoShowCreditPenalty = false. Seguro
            // chamar sem checar status anterior aqui: o filtro de "candidates" acima já garante
            // que só entram reservas que estavam "confirmed", nunca reprocessa a mesma falta.
            await noShowPenaltyService.ApplyAsync(booking.StudentId, ct);

            await auditService.LogAsync(
                "booking.auto_no_show", "Booking", booking.Id, null,
                $"Marcado automaticamente por tolerância vencida (Settings.ToleranceMinutes={settings.ToleranceMinutes}).",
                ct: ct);

            // Libera a vaga pra próxima pessoa da lista de espera — mesmo serviço usado nos
            // outros 3 caminhos que liberam vaga, agora manda e-mail de verdade também aqui.
            await waitlistPromotionService.PromoteNextAsync(
                booking.ClassId, booking.Class?.Name ?? "sua aula", ct);
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "ToleranceEnforcementService: {Count} reserva(s) marcada(s) como no_show automaticamente.",
            overdue.Count);
    }
}
