using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sinchrony.Infrastructure.Services;

// Cobra a renovação automática de todo pacote recorrente elegível a cada ~5 minutos, e reconcilia
// renovações "pending" cujo webhook de confirmação/recusa se perdeu. Mesmo molde do
// PackageExpirationService: BackgroundService puro, um escopo (DbContext) por item, kill switch
// por config (útil pra desligar sem redeploy, ex. durante uma manutenção da Asaas).
public class RecurringRenewalJob(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<RecurringRenewalJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    // Dá tempo do webhook chegar normalmente antes de considerar uma renovação "pending" perdida.
    private static readonly TimeSpan StalePendingThreshold = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!configuration.GetValue("RecurringRenewal:Enabled", true))
                continue;

            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "RecurringRenewalJob: falha ao processar renovações.");
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        List<Guid> dueIds;
        using (var scope = scopeFactory.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<RecurringRenewalService>();
            dueIds = await service.ListDueAsync(ct);
        }

        var processed = 0;
        foreach (var id in dueIds)
        {
            // Um escopo por pacote: uma falha na Asaas (ou no banco) não derruba o lote inteiro.
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<RecurringRenewalService>();
                await service.ProcessRenewalAsync(id, ct);
                processed++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "RecurringRenewalJob: falha ao renovar StudentPackage {Id}.", id);
            }

            // Pequeno intervalo entre chamadas pra respeitar o rate limit da Asaas.
            await Task.Delay(TimeSpan.FromMilliseconds(300), ct);
        }

        if (processed > 0)
            logger.LogInformation("RecurringRenewalJob: {Count} renovação(ões) processada(s).", processed);

        await ReconcileStalePendingAsync(ct);
    }

    private async Task ReconcileStalePendingAsync(CancellationToken ct)
    {
        var olderThan = DateTime.UtcNow - StalePendingThreshold;

        List<Guid> stalePurchaseIds;
        using (var scope = scopeFactory.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<RecurringRenewalService>();
            stalePurchaseIds = (await service.ListStalePendingAsync(olderThan, ct))
                .Select(p => p.Id).ToList();
        }

        foreach (var purchaseId in stalePurchaseIds)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var purchaseRepository = scope.ServiceProvider
                    .GetRequiredService<Domain.Interfaces.Repositories.IPurchaseRepository>();
                var service = scope.ServiceProvider.GetRequiredService<RecurringRenewalService>();

                var stale = await purchaseRepository.GetByIdAsync(purchaseId, ct);
                if (stale is not null)
                    await service.ReconcilePendingAsync(stale, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "RecurringRenewalJob: falha ao reconciliar Purchase {Id}.", purchaseId);
            }
        }
    }
}
