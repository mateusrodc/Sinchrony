namespace Sinchrony.Domain.Interfaces.Services;

/// <summary>
/// Aplica a regra de NoShowCreditPenalty (cascata Package → PackageType → padrão true) sempre
/// que uma reserva é marcada como no_show. Até aqui o campo era só configurável, sem efeito real
/// — toda falta sempre mantinha o crédito consumido (DEMANDA_REGRAS_PACOTE_SEM_EFEITO_BACKEND.md,
/// item 2). Chamado nos 4 lugares que marcam falta: ErpBookingsController.NoShow,
/// UpdateAttendanceCommand, BulkUpdateAttendanceCommand e ToleranceEnforcementService.
///
/// Termos v4, cl. 11.2: FreeNoShowsPerPackage (cascata Package → PackageType → 0) define quantas
/// faltas dentro da validade do pacote ainda devolvem o crédito. A contagem é por pessoa (titular
/// e cada dependente separadamente) e considera as faltas já gravadas no banco — por isso a falta
/// que está sendo processada precisa estar persistida antes de chamar ApplyAsync/ReverseAsync.
/// </summary>
public interface INoShowPenaltyService
{
    /// <summary>
    /// Devolve 1 crédito quando o pacote ativo do aluno tiver NoShowCreditPenalty = false, ou
    /// quando esta falta ainda estiver dentro das faltas perdoadas do pacote
    /// (FreeNoShowsPerPackage). Caso contrário (padrão, comportamento atual preservado), não faz
    /// nada. Persiste sozinho — quem chamar não precisa dar SaveAsync adicional pra este efeito.
    /// </summary>
    Task ApplyAsync(Guid studentId, Guid? dependentId = null, CancellationToken ct = default);

    /// <summary>
    /// Desfaz ApplyAsync quando uma falta é revertida (já persistida como não-falta): se o pacote
    /// tinha devolvido o crédito, retira 1 de novo. Devolve false só quando havia crédito a
    /// retirar e o aluno não tem saldo.
    /// </summary>
    Task<bool> ReverseAsync(Guid studentId, Guid? dependentId = null, CancellationToken ct = default);
}
