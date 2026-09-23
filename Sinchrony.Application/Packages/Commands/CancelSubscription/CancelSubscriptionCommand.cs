using MediatR;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Packages.Commands.CancelSubscription;

// Aluno desiste da renovação automática (não cobra o próximo ciclo). O pacote continua válido
// normalmente até o EndDate atual — CancelRenewal() não mexe em Status/EndDate, só desliga
// AutoRenew. Créditos já concedidos não são estornados (mesma regra do cancelamento manual
// pelo admin).
public record CancelSubscriptionCommand(Guid UserId) : IRequest<Unit>;

public class CancelSubscriptionCommandHandler(
    IStudentPackageRepository studentPackageRepository,
    IUserRepository userRepository,
    IAsaasService asaasService) : IRequestHandler<CancelSubscriptionCommand, Unit>
{
    public async Task<Unit> Handle(CancelSubscriptionCommand request, CancellationToken ct)
    {
        var studentPackage = await studentPackageRepository.GetSubscriptionByStudentAsync(request.UserId, ct)
            ?? throw DomainException.NotFound("Você não possui uma assinatura recorrente ativa.");

        // Legado: se ainda vem de uma assinatura Asaas, encerra a cobrança por lá primeiro — se
        // falhar, não cancela localmente. O modelo atual (AutoRenew) não depende da Asaas pra
        // isso, é só cobrança direta no cartão salvo.
        if (studentPackage.AsaasSubscriptionId is not null)
            await asaasService.CancelSubscriptionAsync(studentPackage.AsaasSubscriptionId, ct);

        studentPackage.CancelRenewal();
        await studentPackageRepository.SaveAsync(ct);

        // Desistir da renovação não é "resolver o pagamento", mas manter o aluno bloqueado por
        // um pacote que ele já decidiu não renovar não faz sentido — o pacote segue válido até o
        // EndDate e expira normalmente dali pra frente.
        var user = await userRepository.GetByIdAsync(request.UserId, ct);
        if (user is { Status: StudentStatus.blocked, BlockedReason: "payment_failed" })
        {
            user.Reactivate();
            await userRepository.SaveAsync(ct);
        }

        return Unit.Value;
    }
}
