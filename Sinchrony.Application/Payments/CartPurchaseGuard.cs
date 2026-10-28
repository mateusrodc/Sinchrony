using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Payments;

// Mesma checagem da compra de pacote único, aplicada ANTES de gerar o Pix ou cobrar o cartão no
// carrinho: pacote com estratégia "block" é recusado (409 ACTIVE_PACKAGE_EXISTS) se o aluno já
// tem pacote ativo ou se outro item do próprio carrinho vai virar o ativo. Simula a mesma ordem e
// as mesmas regras de PurchasePackageService (um plano não avulso substitui uma avulsa ativa —
// ReplacesActive — e nunca é bloqueado por ela).
public static class CartPurchaseGuard
{
    // Ordem em que os itens do carrinho são concedidos (estável dentro de cada grupo): pacotes "block"
    // primeiro, depois os avulsos, depois os planos. Com a configuração real (1ª Experiência é avulsa +
    // block; Aula Avulsa é avulsa + sum_credits) isso deixa a Aula Avulsa somar créditos ao pacote
    // ativo em vez de ser a ativa que bloqueia a 1ª Experiência. Webhook e cartão usam a mesma ordem.
    public static int GrantRank(Package package)
        => package.PurchaseStrategy == "block" ? 0 : package.IsSingleClass ? 1 : 2;

    public static IEnumerable<Package> GrantOrder(IEnumerable<Package> packages)
        => packages.OrderBy(GrantRank);

    public static async Task EnsureCanBuyAsync(
        IStudentPackageRepository studentPackageRepository, Guid studentId,
        IReadOnlyList<Package> packages, CancellationToken ct)
    {
        var active = await studentPackageRepository.GetActiveByStudentAsync(studentId, ct);
        EnsureCanBuy(active?.Package, hadActiveBeforeCart: active is not null, packages);
    }

    public static void EnsureCanBuy(Package? activePackage, bool hadActiveBeforeCart, IReadOnlyList<Package> packages)
    {
        var hasActive = hadActiveBeforeCart;
        var current = activePackage;
        var currentIsFromCart = false;

        foreach (var package in GrantOrder(packages))
        {
            if (hasActive)
            {
                if (package.ReplacesActive(current))
                {
                    current = package;
                    currentIsFromCart = true;
                    continue;
                }

                if (package.PurchaseStrategy == "block")
                    throw DomainException.Conflict("ACTIVE_PACKAGE_EXISTS",
                        currentIsFromCart && current is not null
                            ? $"O pacote \"{package.Name}\" não pode ser comprado junto com \"{current.Name}\": só é permitido um pacote ativo por vez."
                            : $"O pacote \"{package.Name}\" não pode ser comprado enquanto você tiver um pacote ativo.");
                continue;
            }

            hasActive = true;
            current = package;
            currentIsFromCart = true;
        }
    }
}
