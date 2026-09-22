using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sinchrony.Infrastructure.Services;

// Roda a cada minuto e expira pacotes vencidos (ver StudentPackageLifecycleService pra regra).
// Mesmo molde do ToleranceEnforcementService: BackgroundService puro, sem dependência de job.
// Kill switch: "PackageExpiration:Enabled" = false desliga o processamento sem redeploy de código
// (útil no primeiro deploy, que expira de uma vez todo pacote que já estava vencido).
public class PackageExpirationService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<PackageExpirationService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!configuration.GetValue("PackageExpiration:Enabled", true))
                continue;

            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "PackageExpirationService: falha ao processar expiração de pacotes.");
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        List<Guid> ids;
        using (var scope = scopeFactory.CreateScope())
        {
            var lifecycle = scope.ServiceProvider.GetRequiredService<StudentPackageLifecycleService>();
            ids = await lifecycle.ListExpiredIdsAsync(now, ct);
        }

        var expiredCount = 0;
        foreach (var id in ids)
        {
            // Um escopo (DbContext) por pacote: falha em um aluno não contamina os demais.
            try
            {
                using var scope = scopeFactory.CreateScope();
                var lifecycle = scope.ServiceProvider.GetRequiredService<StudentPackageLifecycleService>();
                if (await lifecycle.ExpireAsync(id, now, ct))
                    expiredCount++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "PackageExpirationService: falha ao expirar StudentPackage {Id}.", id);
            }
        }

        if (expiredCount > 0)
            logger.LogInformation("PackageExpirationService: {Count} pacote(s) expirado(s).", expiredCount);
    }
}
