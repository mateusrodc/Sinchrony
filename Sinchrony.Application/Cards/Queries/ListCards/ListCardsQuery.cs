using MediatR;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Cards.Queries.ListCards;

public record ListCardsQuery(Guid UserId) : IRequest<IEnumerable<CardDto>>;

public record CardDto(
    Guid Id, string LastDigits, string Brand,
    string HolderName, string ExpiryDate,
    bool IsDefault, string? Nickname, string Token);

public class ListCardsQueryHandler(ICardRepository cardRepository)
    : IRequestHandler<ListCardsQuery, IEnumerable<CardDto>>
{
    public async Task<IEnumerable<CardDto>> Handle(ListCardsQuery request, CancellationToken ct)
    {
        var cards = await cardRepository.ListByUserAsync(request.UserId, ct);
        return cards.Select(MapToDto);
    }

    public static CardDto MapToDto(Domain.Entities.Card c) =>
        new(c.Id, c.LastDigits, c.Brand, c.HolderName,
            c.ExpiryDate, c.IsDefault, c.Nickname, c.Token);
}