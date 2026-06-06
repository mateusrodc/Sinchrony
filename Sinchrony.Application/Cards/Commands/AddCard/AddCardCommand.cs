using MediatR;
using Sinchrony.Application.Cards.Queries.ListCards;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Cards.Commands.AddCard;

public record AddCardCommand(
    Guid UserId, string Number, string HolderName,
    string ExpiryDate, string Cvv, string? Nickname) : IRequest<CardDto>;

public class AddCardCommandHandler(
    ICardRepository cardRepository,
    IUserRepository userRepository,
    IAsaasService asaasService)
    : IRequestHandler<AddCardCommand, CardDto>
{
    public async Task<CardDto> Handle(AddCardCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw DomainException.NotFound("User not found.");

        var customerId = await asaasService.GetOrCreateCustomerAsync(user.Name, user.Email, ct: ct);
        var tokenResult = await asaasService.TokenizeCardAsync(
            request.Number, request.HolderName,
            request.ExpiryDate, request.Cvv, customerId, ct);

        var duplicate = await cardRepository.ExistsByTokenAsync(request.UserId, tokenResult.Token, ct);
        if (duplicate)
            throw DomainException.Conflict("CARD_DUPLICATE", "Card already registered.");

        var card = Card.Create(request.UserId, tokenResult.LastDigits, tokenResult.Brand,
            request.HolderName, request.ExpiryDate, tokenResult.Token, request.Nickname);

        var existing = await cardRepository.ListByUserAsync(request.UserId, ct);
        if (!existing.Any()) card.SetAsDefault();

        await cardRepository.AddAsync(card, ct);
        await cardRepository.SaveAsync(ct);

        return ListCardsQueryHandler.MapToDto(card);
    }
}