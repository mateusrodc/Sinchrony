namespace Sinchrony.Domain.Interfaces.Services;

/// <summary>
/// Libera uma vaga pro próximo da lista de espera de uma aula (Cláusula 8.2/8.3 do Termo).
/// Serviço único usado por todo caminho que pode liberar vaga — cancelamento pelo aluno (App),
/// cancelamento/no-show manual pela equipe (ERP) e no-show automático por tolerância — pra não
/// duplicar a lógica de notificar + mandar e-mail em cada um deles.
/// </summary>
public interface IWaitlistPromotionService
{
    Task PromoteNextAsync(Guid classId, string className, CancellationToken ct = default);
}
