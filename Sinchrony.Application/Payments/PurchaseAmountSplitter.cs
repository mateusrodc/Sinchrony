using Sinchrony.Domain.Entities;

namespace Sinchrony.Application.Payments;

// Carrinho com vários pacotes gera UMA cobrança na Asaas, mas uma Purchase por pacote. Cada
// Purchase precisa guardar a sua parte (preço do pacote, já com a fatia do desconto do cupom) —
// gravar o total em todas dobrava Cobranças e faturamento. A soma das partes é sempre o total
// cobrado: a última recebe o resto dos arredondamentos.
public static class PurchaseAmountSplitter
{
    public static IReadOnlyList<decimal> Split(IReadOnlyList<Package> packages, decimal chargedTotal)
    {
        if (packages.Count == 0) return [];
        if (packages.Count == 1) return [chargedTotal];

        var subtotal = packages.Sum(p => p.Price);
        var parts = new List<decimal>(packages.Count);
        var allocated = 0m;

        for (var i = 0; i < packages.Count; i++)
        {
            var part = i == packages.Count - 1
                ? chargedTotal - allocated
                : subtotal > 0
                    ? Math.Round(chargedTotal * packages[i].Price / subtotal, 2, MidpointRounding.AwayFromZero)
                    : Math.Round(chargedTotal / packages.Count, 2, MidpointRounding.AwayFromZero);
            parts.Add(part);
            allocated += part;
        }

        return parts;
    }
}
