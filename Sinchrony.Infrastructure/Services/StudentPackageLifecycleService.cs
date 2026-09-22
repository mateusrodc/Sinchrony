using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Infrastructure.Persistence;

namespace Sinchrony.Infrastructure.Services;

// Regra de negócio (21/09/2026): quando o pacote vence, os créditos restantes expiram junto —
// ex.: pacote de 8 créditos com validade de 30 dias. Até então nada expirava StudentPackage
// (Expire() nunca era chamado), então o saldo valia pra sempre e a fila de pacotes ("queue")
// nunca andava fora do fluxo de webhook do PIX.
//
// Ao expirar um pacote ativo:
//   1. marca o pacote como expired e zera as alocações;
//   2. zera o saldo do aluno (User.Credits) com uma CreditTransaction "package_expiration";
//   3. promove o pacote mais antigo da fila (ativa, cria alocações e credita).
//
// Pacotes na fila que vieram de PIX são tratados à parte, porque o webhook do Asaas já credita
// o saldo na confirmação mesmo quando o pacote fica na fila (WebhooksController): confirmado =
// créditos já estão no saldo (preservados na expiração e não creditados de novo na promoção);
// pendente = ainda não foi pago, não é promovido.
public class StudentPackageLifecycleService(ApplicationDbContext db)
{
    public async Task<List<Guid>> ListExpiredIdsAsync(DateTime now, CancellationToken ct = default)
        => await db.StudentPackages
            .Where(sp => sp.Status == StudentPackageStatus.active && sp.EndDate < now)
            .Select(sp => sp.Id)
            .ToListAsync(ct);

    // Retorna false se o pacote não está mais elegível (já expirado por outra execução, ou
    // estendido/cancelado desde que o id foi listado).
    public async Task<bool> ExpireAsync(Guid studentPackageId, DateTime now, CancellationToken ct = default)
    {
        var sp = await db.StudentPackages
            .Include(x => x.Package)
            .Include(x => x.Allocations)
            .FirstOrDefaultAsync(x => x.Id == studentPackageId
                && x.Status == StudentPackageStatus.active
                && x.EndDate < now, ct);
        if (sp is null) return false;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == sp.StudentId, ct);

        sp.Expire();
        foreach (var alloc in sp.Allocations)
            alloc.Debit(alloc.CreditsRemaining);

        var queue = await db.StudentPackages
            .Include(x => x.Package)
            .Where(x => x.StudentId == sp.StudentId && x.Status == StudentPackageStatus.queued)
            .OrderBy(x => x.PurchasedAt)
            .ToListAsync(ct);

        var pixPurchases = queue.Count == 0
            ? []
            : await db.Purchases
                .Where(p => p.UserId == sp.StudentId && p.PaymentMethod == "pix")
                .ToListAsync(ct);

        bool IsPixPrepaid(StudentPackage q) =>
            pixPurchases.Any(p => p.PackageId == q.PackageId && p.Status == "confirmed");
        bool IsPixPending(StudentPackage q) =>
            !IsPixPrepaid(q) && pixPurchases.Any(p => p.PackageId == q.PackageId);

        if (user is not null)
        {
            var prepaidCredits = queue.Where(IsPixPrepaid)
                .Sum(q => q.Package?.GetCreditsToGrant() ?? 0);

            var expired = user.ExpireCredits(keep: prepaidCredits);
            if (expired > 0)
            {
                db.CreditTransactions.Add(CreditTransaction.Create(
                    user.Id, -expired, user.Credits,
                    Truncate($"Créditos expirados: {sp.Package?.Name ?? "pacote"} venceu em {sp.EndDate:dd/MM/yyyy}"),
                    "package_expiration", sp.Id));
            }
        }

        var next = queue.FirstOrDefault(q => !IsPixPending(q) && q.Package is not null);
        if (next is not null)
            await PromoteAsync(next, user, IsPixPrepaid(next), now, ct);

        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task PromoteAsync(
        StudentPackage next, User? user, bool creditsAlreadyInBalance, DateTime now, CancellationToken ct)
    {
        var package = next.Package!;
        next.Activate();

        var dependents = await db.Dependents
            .Where(d => d.ResponsibleStudentId == next.StudentId && d.Active)
            .ToListAsync(ct);

        var creditsPerPerson = package.GetCreditsPerPerson(1 + dependents.Count);
        db.DependentPackageAllocations.Add(DependentPackageAllocation.Create(next.Id, null, creditsPerPerson));
        if (package.MaxDependents > 0)
        {
            foreach (var dep in dependents)
                db.DependentPackageAllocations.Add(
                    DependentPackageAllocation.Create(next.Id, dep.Id, creditsPerPerson));
        }

        if (creditsAlreadyInBalance || user is null) return;

        var credits = package.GetCreditsToGrant();
        if (credits <= 0) return;

        user.AddCredits(credits);
        next.SetSource(next.Source, credits);
        db.CreditTransactions.Add(CreditTransaction.Create(
            user.Id, credits, user.Credits,
            Truncate($"Pacote {package.Name} ativado da fila"),
            "queue_activation", next.Id));
    }

    // credit_transactions.Reason tem limite de 200 caracteres.
    private static string Truncate(string s) => s.Length <= 200 ? s : s[..200];
}
