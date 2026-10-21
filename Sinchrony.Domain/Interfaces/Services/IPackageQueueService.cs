namespace Sinchrony.Domain.Interfaces.Services;

/// <summary>
/// Regra 2 de DEMANDA_VALIDADE_FIM_DO_DIA_E_FILA_AO_ZERAR_CREDITOS_BACKEND.md (21/10/2026): o
/// pacote da fila entra assim que os créditos do ativo acabam, sem esperar a data do ativo.
/// Chamado logo depois de cada operação que reduz o saldo (reserva, ajuste manual para baixo,
/// falta desfeita que retira o crédito devolvido), dentro da mesma transação; o
/// PackageExpirationService repete a checagem a cada minuto como rede de proteção.
/// </summary>
public interface IPackageQueueService
{
    /// <summary>
    /// Se o aluno tem pacote ativo (não recorrente) com o saldo zerado — e, no plano família,
    /// também todas as cotas dos dependentes zeradas — e um pacote elegível na fila (Pix não
    /// confirmado não é elegível), encerra o ativo e promove o da fila na hora. Persiste sozinho.
    /// Devolve true quando promoveu.
    /// </summary>
    Task<bool> PromoteIfCreditsExhaustedAsync(Guid studentId, CancellationToken ct = default);
}
