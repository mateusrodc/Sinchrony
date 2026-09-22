using MediatR;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Packages.Commands.UpdateSubscriptionCard;

// Aluno bloqueado (ou prestes a ser) regulariza a forma de pagamento da própria assinatura
// recorrente, sem precisar comprar o pacote de novo. A próxima cobrança que a Asaas confirmar
// pra essa mesma assinatura é quem desbloqueia o acesso (via webhook), não este endpoint.
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

        var active = await studentPackageRepository.GetActiveByStudentAsync(request.UserId, ct);
        var queued = await studentPackageRepository.GetQueuedByStudentAsync(request.UserId, ct);
        var studentPackage = active?.AsaasSubscriptionId is not null ? active
            : queued?.AsaasSubscriptionId is not null ? queued
            : null;

        if (studentPackage?.AsaasSubscriptionId is null)
            throw DomainException.NotFound("Você não possui uma assinatura recorrente ativa.");

        await asaasService.UpdateSubscriptionCardAsync(studentPackage.AsaasSubscriptionId, card.Token, ct);

        return Unit.Value;
    }
}
