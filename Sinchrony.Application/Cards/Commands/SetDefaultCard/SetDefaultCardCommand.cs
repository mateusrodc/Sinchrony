using MediatR;
using Sinchrony.Application.Cards.Queries.ListCards;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Cards.Commands.SetDefaultCard;

public record SetDefaultCardCommand(Guid UserId, Guid CardId) : IRequest<IEnumerable<CardDto>>;

public class SetDefaultCardCommandHandler(ICardRepository cardRepository)
    : IRequestHandler<SetDefaultCardCommand, IEnumerable<CardDto>>
{
    public async Task<IEnumerable<CardDto>> Handle(SetDefaultCardCommand request, CancellationToken ct)
    {
        var card = await cardRepository.GetByIdAsync(request.CardId, ct)
            ?? throw DomainException.NotFound("Card not found.");

        if (card.UserId != request.UserId)
            throw DomainException.Forbidden("This card does not belong to you.");

        var all = (await cardRepository.ListByUserAsync(request.UserId, ct)).ToList();
        foreach (var c in all) c.RemoveDefault();
        card.SetAsDefault();

        await cardRepository.SaveAsync(ct);
        return all.Select(ListCardsQueryHandler.MapToDto);
    }
}