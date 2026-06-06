using MediatR;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Cards.Commands.RemoveCard;

public record RemoveCardCommand(Guid UserId, Guid CardId) : IRequest;

public class RemoveCardCommandHandler(ICardRepository cardRepository)
    : IRequestHandler<RemoveCardCommand>
{
    public async Task Handle(RemoveCardCommand request, CancellationToken ct)
    {
        var card = await cardRepository.GetByIdAsync(request.CardId, ct)
            ?? throw DomainException.NotFound("Card not found.");

        if (card.UserId != request.UserId)
            throw DomainException.Forbidden("This card does not belong to you.");

        var all = (await cardRepository.ListByUserAsync(request.UserId, ct)).ToList();

        if (all.Count == 1)
            throw DomainException.Validation("ONLY_CARD", "Cannot remove the only card.");

        if (card.IsDefault)
        {
            var oldest = all
                .Where(c => c.Id != card.Id)
                .OrderBy(c => c.CreatedAt)
                .First();
            oldest.SetAsDefault();
        }

        await cardRepository.RemoveAsync(card, ct);
        await cardRepository.SaveAsync(ct);
    }
}