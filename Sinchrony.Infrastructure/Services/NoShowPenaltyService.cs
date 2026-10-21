using Microsoft.Extensions.Logging;
using Sinchrony.Application.Common;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Domain.Services;

namespace Sinchrony.Infrastructure.Services;

public class NoShowPenaltyService(
    IStudentPackageRepository studentPackageRepository,
    IUserRepository userRepository,
    IBookingRepository bookingRepository,
    IPackageQueueService packageQueueService,
    ILogger<NoShowPenaltyService> logger) : INoShowPenaltyService
{
    public async Task ApplyAsync(Guid studentId, Guid? dependentId = null, CancellationToken ct = default)
    {
        var studentPackage = await studentPackageRepository.GetActiveByStudentAsync(studentId, ct);

        // A falta já está gravada: contando com ela, ainda é perdoada se o total não passou do limite.
        if (!await CreditIsReturnedAsync(studentPackage, studentId, dependentId, afterApply: true, ct))
            return; // penalidade padrão (mantém o crédito consumido) — nada a fazer

        var user = await userRepository.GetByIdAsync(studentId, ct);
        if (user is null)
        {
            logger.LogWarning(
                "NoShowPenaltyService: aluno {StudentId} não encontrado ao tentar devolver crédito.",
                studentId);
            return;
        }

        user.AddCredits(1);
        await userRepository.SaveAsync(ct);
    }

    public async Task<bool> ReverseAsync(Guid studentId, Guid? dependentId = null, CancellationToken ct = default)
    {
        var studentPackage = await studentPackageRepository.GetActiveByStudentAsync(studentId, ct);

        // A falta desfeita já saiu da contagem: ela tinha devolvido o crédito se, com ela, o total
        // ficava dentro do limite — ou seja, se as faltas restantes são menos que o limite.
        if (!await CreditIsReturnedAsync(studentPackage, studentId, dependentId, afterApply: false, ct))
            return true; // a falta não devolveu crédito, nada a retirar

        var user = await userRepository.GetByIdAsync(studentId, ct);
        if (user is null || user.Credits < 1)
        {
            logger.LogWarning(
                "NoShowPenaltyService: não foi possível retirar o crédito devolvido do aluno {StudentId} ao desfazer a falta.",
                studentId);
            return false;
        }

        user.DeductCredits(1);
        await userRepository.SaveAsync(ct);
        // Retirar o crédito devolvido pode zerar o saldo do pacote ativo: o da fila entra na hora.
        await packageQueueService.PromoteIfCreditsExhaustedAsync(studentId, ct);
        return true;
    }

    private async Task<bool> CreditIsReturnedAsync(
        StudentPackage? studentPackage, Guid studentId, Guid? dependentId, bool afterApply, CancellationToken ct)
    {
        if (!PackageRuleResolver.GetNoShowCreditPenalty(studentPackage))
            return true; // NoShowCreditPenalty = false: nunca perde o crédito

        var freeNoShows = PackageRuleResolver.GetFreeNoShowsPerPackage(studentPackage);
        if (freeNoShows <= 0 || studentPackage is null)
            return false;

        var since = BrasiliaTime.ToDate(studentPackage.StartDate);
        var noShows = await bookingRepository.CountNoShowsSinceAsync(studentId, dependentId, since, ct);

        return afterApply ? noShows <= freeNoShows : noShows < freeNoShows;
    }
}
