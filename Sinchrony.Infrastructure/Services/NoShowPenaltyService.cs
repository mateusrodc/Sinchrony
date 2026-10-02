using Microsoft.Extensions.Logging;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Domain.Services;

namespace Sinchrony.Infrastructure.Services;

public class NoShowPenaltyService(
    IStudentPackageRepository studentPackageRepository,
    IUserRepository userRepository,
    ILogger<NoShowPenaltyService> logger) : INoShowPenaltyService
{
    public async Task ApplyAsync(Guid studentId, CancellationToken ct = default)
    {
        var studentPackage = await studentPackageRepository.GetActiveByStudentAsync(studentId, ct);
        if (PackageRuleResolver.GetNoShowCreditPenalty(studentPackage))
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

    public async Task<bool> ReverseAsync(Guid studentId, CancellationToken ct = default)
    {
        var studentPackage = await studentPackageRepository.GetActiveByStudentAsync(studentId, ct);
        if (PackageRuleResolver.GetNoShowCreditPenalty(studentPackage))
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
        return true;
    }
}
