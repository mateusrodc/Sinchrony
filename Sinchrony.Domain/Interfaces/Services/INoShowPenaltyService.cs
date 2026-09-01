namespace Sinchrony.Domain.Interfaces.Services;

/// <summary>
/// Aplica a regra de NoShowCreditPenalty (cascata Package → PackageType → padrão true) sempre
/// que uma reserva é marcada como no_show. Até aqui o campo era só configurável, sem efeito real
/// — toda falta sempre mantinha o crédito consumido (DEMANDA_REGRAS_PACOTE_SEM_EFEITO_BACKEND.md,
/// item 2). Chamado nos 4 lugares que marcam falta: ErpBookingsController.NoShow,
/// UpdateAttendanceCommand, BulkUpdateAttendanceCommand e ToleranceEnforcementService.
/// </summary>
public interface INoShowPenaltyService
{
    /// <summary>
    /// Se o pacote ativo do aluno tiver NoShowCreditPenalty resolvido como false, devolve 1
    /// crédito. Caso contrário (padrão true, comportamento atual preservado), não faz nada.
    /// Persiste sozinho — quem chamar não precisa dar SaveAsync adicional pra este efeito.
    /// </summary>
    Task ApplyAsync(Guid studentId, CancellationToken ct = default);
}
