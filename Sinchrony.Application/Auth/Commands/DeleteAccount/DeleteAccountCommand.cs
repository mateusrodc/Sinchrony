using MediatR;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Auth.Commands.DeleteAccount;

/// <summary>
/// Autoexclusão de conta pelo próprio usuário autenticado (Apple Guideline 5.1.1(v) / LGPD).
/// </summary>
public record DeleteAccountCommand(Guid UserId, string? CurrentPassword) : IRequest;

public class DeleteAccountCommandHandler(
    IUserRepository userRepository,
    IPasswordService passwordService,
    ICardRepository cardRepository,
    IDependentRepository dependentRepository) : IRequestHandler<DeleteAccountCommand>
{
    public async Task Handle(DeleteAccountCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw DomainException.NotFound("User not found.");

        // Contas criadas via Google nunca têm uma senha real (o hash é um GUID aleatório
        // gerado em User.CreateWithGoogle), então não há senha pra confirmar — a própria
        // sessão autenticada (JWT) já é a confirmação nesse caso. Para contas com senha,
        // a senha atual é exigida para evitar exclusão acidental.
        var isGoogleAccount = !string.IsNullOrEmpty(user.GoogleId);
        if (!isGoogleAccount)
        {
            if (string.IsNullOrEmpty(request.CurrentPassword) ||
                !passwordService.VerifyPassword(request.CurrentPassword, user.PasswordHash))
                throw DomainException.Validation("INVALID_PASSWORD", "Senha incorreta.");
        }

        // Remove cartões salvos (tokens de gateway) para evitar qualquer tentativa de
        // cobrança futura. Não é preciso reter esses dados: o histórico financeiro já fica
        // registrado em Purchase (que não referencia Card), atendendo à obrigação fiscal.
        var cards = await cardRepository.ListByUserAsync(user.Id, ct);
        foreach (var card in cards)
            await cardRepository.RemoveAsync(card, ct);
        await cardRepository.SaveAsync(ct);

        // Se o usuário é responsável por dependentes, desfaz o vínculo (não afeta a conta
        // do dependente, só o "guarda-chuva" que aponta pra uma conta responsável excluída).
        var dependents = await dependentRepository.ListByStudentAsync(user.Id, ct);
        foreach (var dependent in dependents)
        {
            dependent.Deactivate();
            if (dependent.UserId is { } dependentUserId)
            {
                var dependentUser = await userRepository.GetByIdAsync(dependentUserId, ct);
                dependentUser?.ClearDependent();
            }
        }

        // Se o próprio usuário é um dependente, desativa o registro correspondente do lado
        // do responsável, pra não sobrar um dependente "fantasma" ativo na lista dele.
        var ownDependentLink = await dependentRepository.GetByUserIdAsync(user.Id, ct);
        ownDependentLink?.Deactivate();
        await dependentRepository.SaveAsync(ct);

        var anonymizedHash = passwordService.HashPassword(Guid.NewGuid().ToString("N"));
        user.AnonymizeForDeletion(anonymizedHash);

        foreach (var token in user.RefreshTokens.Where(t => !t.Revoked))
            token.Revoke();

        await userRepository.SaveAsync(ct);
    }
}
