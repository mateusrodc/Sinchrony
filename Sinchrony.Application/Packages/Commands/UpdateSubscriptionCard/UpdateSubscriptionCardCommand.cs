using MediatR;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Packages.Commands.UpdateSubscriptionCard;

// Aluno bloqueado (ou prestes a ser) regulariza a forma de pagamento do próprio pacote
// recorrente, sem precisar comprar de novo. Se estava retrying/overdue, sinaliza pro job de
// renovação tentar cobrar de novo já no próximo tick — não desbloqueia direto aqui.
public record UpdateSubscriptionCardCommand(Guid UserId, Guid CardId) : IRequest<Unit>;

public class UpdateSubscriptionCardCommandHandler(
    ICardRepository cardRepository,
    IStudentPackageRepository studentPackageRepository,
    IAsaasService asaasService) : IRequestHandler<UpdateSubscriptionCardCommand, Unit>
{
    public async Task<Unit> Handle(UpdateSubscriptionCardCommand request, CancellationToken ct)
    {
        var card = await cardRepository.GetByIdAsync(request.CardId, ct)
            ?? throw DomainException.NotFound("Card not found.");

        if (card.UserId != request.UserId)
            throw DomainException.Forbidden("Este cartão não pertence a você.");

        var studentPackage = await studentPackageRepository.GetSubscriptionByStudentAsync(request.UserId, ct)
            ?? throw DomainException.NotFound("Você não possui uma assinatura recorrente ativa.");

        // Legado: se ainda vem de uma assinatura Asaas, atualiza o cartão por lá também.
        if (studentPackage.AsaasSubscriptionId is not null)
            await asaasService.UpdateSubscriptionCardAsync(studentPackage.AsaasSubscriptionId, card.Token, ct);

        studentPackage.SetRenewalCard(card.Id);
        await studentPackageRepository.SaveAsync(ct);

        return Unit.Value;
    }
}
