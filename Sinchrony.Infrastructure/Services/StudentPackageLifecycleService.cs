using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Infrastructure.Persistence;

namespace Sinchrony.Infrastructure.Services;

// Regra de negócio (21/09/2026, ampliada 23/09/2026 — Termos de Uso 6.3): quando um ciclo de
// crédito termina — pacote avulso vencendo, ou o ciclo de uma renovação automática virando —
// os créditos não usados expiram, sem acumular pro ciclo seguinte. Dois casos:
//
//   1. Pacote sem renovação (ou renovação não paga): ExpireAsync — expira de vez (Status =
//      expired), zera o saldo/cotas e promove o próximo da fila, se houver.
//   2. Renovação automática já paga aguardando o EndDate virar: TurnoverAsync — o MESMO
//      StudentPackage continua (Status volta a active), zera o saldo/cotas do ciclo que terminou
//      e credita/recria as cotas do ciclo novo. Chamado por este serviço (pagamento confirmado
//      antes do EndDate, espera a virada) OU direto por RecurringRenewalService (pagamento
//      confirmado depois do EndDate — a virada acontece na hora, não espera o job).
//
// Os dois casos compartilham a mecânica de "expira o que sobrou" e "credita o ciclo novo" via
// ExpireCycleCreditsAsync/GrantCycleCreditsAsync, pra não divergir de novo.
public class StudentPackageLifecycleService(ApplicationDbContext db)
{
    public async Task<List<Guid>> ListExpiredIdsAsync(DateTime now, CancellationToken ct = default)
        => await db.StudentPackages
            .Where(sp => sp.Status == StudentPackageStatus.active && sp.EndDate < now)
            // Pacote recorrente (AutoRenew, ou legado AsaasSubscriptionId) com cobrança em
            // retentativa ou vencida (o aluno já está bloqueado nesse caso) não deve zerar os
            // créditos por passar da data — a renovação ou o cancelamento é quem resolve esse
            // estado, não a expiração.
            .Where(sp => (!sp.AutoRenew && sp.AsaasSubscriptionId == null)
                || (sp.PaymentStatus != SubscriptionPaymentStatus.retrying
                    && sp.PaymentStatus != SubscriptionPaymentStatus.overdue))
            // Uma renovação "pending" (antifraude) pode levar mais de 24h pra confirmar — não
            // expira um pacote com cobrança em andamento, senão o aluno paga e fica sem pacote
            // mesmo assim (ChargeRenewed reativa o Status, mas só quando a confirmação chegar).
            .Where(sp => !db.Purchases.Any(p =>
                p.StudentPackageId == sp.Id && p.Kind == "renewal" && p.Status == "pending"))
            // Renovação já paga pro ciclo que está terminando — vira (TurnoverAsync), não expira.
            .Where(sp => !sp.RenewalPaidForCycle)
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
                && x.EndDate < now
                && ((!x.AutoRenew && x.AsaasSubscriptionId == null)
                    || (x.PaymentStatus != SubscriptionPaymentStatus.retrying
                        && x.PaymentStatus != SubscriptionPaymentStatus.overdue))
                && !x.RenewalPaidForCycle
                && !db.Purchases.Any(p =>
                    p.StudentPackageId == x.Id && p.Kind == "renewal" && p.Status == "pending"), ct);
        if (sp is null) return false;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == sp.StudentId, ct);

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

        var prepaidCredits = queue.Where(IsPixPrepaid).Sum(q => q.Package?.GetCreditsToGrant() ?? 0);

        sp.Expire();
        ExpireCycleCredits(sp, user, keep: prepaidCredits,
            $"Créditos expirados: {sp.Package?.Name ?? "pacote"} venceu em {sp.EndDate:dd/MM/yyyy}");

        var next = queue.FirstOrDefault(q => !IsPixPending(q) && q.Package is not null);
        if (next is not null)
            await PromoteAsync(next, user, IsPixPrepaid(next), ct);

        await db.SaveChangesAsync(ct);
        return true;
    }

    // Ids com renovação automática já paga cujo ciclo atual venceu — TurnoverAsync (não
    // ExpireAsync) que se aplica: o pacote continua, só troca de ciclo.
    public async Task<List<Guid>> ListDueForTurnoverAsync(DateTime now, CancellationToken ct = default)
        => await db.StudentPackages
            .Where(sp => sp.Status == StudentPackageStatus.active
                && sp.RenewalPaidForCycle
                && sp.EndDate < now)
            .Select(sp => sp.Id)
            .ToListAsync(ct);

    // Retorna false se o pacote não está mais elegível (já virou por outra execução, cancelado
    // ou o EndDate foi prorrogado — atestado — desde que o id foi listado).
    public async Task<bool> TurnoverRenewalAsync(Guid studentPackageId, DateTime now, CancellationToken ct = default)
    {
        var sp = await db.StudentPackages
            .Include(x => x.Package)
            .Include(x => x.Allocations)
            .FirstOrDefaultAsync(x => x.Id == studentPackageId
                && x.Status == StudentPackageStatus.active
                && x.RenewalPaidForCycle
                && x.EndDate < now, ct);
        if (sp is null) return false;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == sp.StudentId, ct);
        await TurnoverAsync(sp, user, now, ct);

        await db.SaveChangesAsync(ct);
        return true;
    }

    // Vira o ciclo de uma renovação automática já paga: expira o saldo/cotas que sobraram do
    // ciclo que terminou, começa o ciclo novo (ChargeRenewed) e credita/recria as cotas dele —
    // sem acumular nada entre ciclos (Termos 6.3). Só muta as entidades — quem chama decide
    // quando salvar (TurnoverRenewalAsync salva sozinho; RecurringRenewalService salva junto com
    // o resto da confirmação de pagamento).
    public async Task TurnoverAsync(StudentPackage sp, User? user, DateTime now, CancellationToken ct)
    {
        var package = sp.Package!;

        ExpireCycleCredits(sp, user, keep: 0,
            $"Créditos expirados: {package.Name} — ciclo encerrado em {sp.EndDate:dd/MM/yyyy}");

        sp.ChargeRenewed(); // novo StartDate/EndDate, Status=active, zera tentativas e RenewalPaidForCycle

        await GrantCycleCreditsAsync(sp, package, user, creditsAlreadyInBalance: false,
            $"Pacote {package.Name} — novo ciclo (renovação automática)", "renewal_cycle", ct);
    }

    private async Task PromoteAsync(
        StudentPackage next, User? user, bool creditsAlreadyInBalance, CancellationToken ct)
    {
        var package = next.Package!;
        next.Activate();

        await GrantCycleCreditsAsync(next, package, user, creditsAlreadyInBalance,
            $"Pacote {package.Name} ativado da fila", "queue_activation", ct);
    }

    // Debita as cotas antigas e expira o saldo restante do aluno (mantendo `keep`, usado só na
    // expiração com fila PIX pré-paga — virada de ciclo nunca tem isso, sempre keep=0).
    private void ExpireCycleCredits(StudentPackage sp, User? user, int keep, string reason)
    {
        foreach (var alloc in sp.Allocations)
            alloc.Debit(alloc.CreditsRemaining);

        if (user is null) return;

        var expired = user.ExpireCredits(keep: keep);
        if (expired > 0)
            db.CreditTransactions.Add(CreditTransaction.Create(
                user.Id, -expired, user.Credits, Truncate(reason), "package_expiration", sp.Id));
    }

    // Recria as cotas do titular e dos dependentes pro ciclo que está começando, e credita o
    // saldo se ainda não tiver sido creditado por outro caminho (PromoteAsync com PIX já
    // confirmado). Remove as cotas antigas do pacote antes — a virada de ciclo é o MESMO
    // StudentPackage vivendo vários ciclos (diferente de promover da fila, que é sempre um
    // pacote sem alocação prévia): sem isso, cada renovação empilharia uma cota nova por
    // dependente, duplicando linhas já zeradas.
    private async Task GrantCycleCreditsAsync(
        StudentPackage sp, Package package, User? user, bool creditsAlreadyInBalance,
        string reason, string transactionType, CancellationToken ct)
    {
        if (sp.Allocations.Count > 0)
            db.DependentPackageAllocations.RemoveRange(sp.Allocations);

        var dependents = await db.Dependents
            .Where(d => d.ResponsibleStudentId == sp.StudentId && d.Active)
            .ToListAsync(ct);

        var creditsPerPerson = package.GetCreditsPerPerson(1 + dependents.Count);
        db.DependentPackageAllocations.Add(DependentPackageAllocation.Create(sp.Id, null, creditsPerPerson));
        if (package.MaxDependents > 0)
        {
            foreach (var dep in dependents)
                db.DependentPackageAllocations.Add(
                    DependentPackageAllocation.Create(sp.Id, dep.Id, creditsPerPerson));
        }

        if (creditsAlreadyInBalance || user is null) return;

        var credits = package.GetCreditsToGrant();
        if (credits <= 0) return;

        user.AddCredits(credits);
        sp.SetSource(sp.Source, credits);
        db.CreditTransactions.Add(CreditTransaction.Create(
            user.Id, credits, user.Credits, Truncate(reason), transactionType, sp.Id));
    }

    // credit_transactions.Reason tem limite de 200 caracteres.
    private static string Truncate(string s) => s.Length <= 200 ? s : s[..200];
}
