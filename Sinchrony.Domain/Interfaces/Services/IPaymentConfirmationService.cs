namespace Sinchrony.Domain.Interfaces.Services;

// Confirma as Purchases pendentes de uma cobrança Asaas paga: credita, ativa o StudentPackage na
// fila ou encadeia a renovação. É o mesmo fluxo do webhook PAYMENT_CONFIRMED/PAYMENT_RECEIVED,
// extraído para também poder ser acionado quando se descobre (consulta direta à Asaas) que uma
// cobrança foi paga e o webhook se perdeu.
public interface IPaymentConfirmationService
{
    Task ConfirmPendingAsync(string transactionId, string eventType, CancellationToken ct = default);
}
