using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Sinchrony.Application.Common;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Services;
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
//
// Desde 21/10/2026 há um terceiro caminho: PromoteIfCreditsExhaustedAsync — o ativo encerra
// quando os créditos acabam (não só pela data) e o próximo da fila entra na hora.
public class StudentPackageLifecycleService(ApplicationDbContext db) : IPackageQueueService
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
        var studentId = await db.StudentPackages
            .Where(x => x.Id == studentPackageId)
            .Select(x => (Guid?)x.StudentId)
            .FirstOrDefaultAsync(ct);
        if (studentId is null) return false;

        await using var tx = await LockStudentAsync(studentId.Value, ct);

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
        var queue = await LoadQueueAsync(sp.StudentId, ct);

        sp.Expire();
        ExpireCycleCredits(sp, user, keep: queue.PrepaidCredits,
            $"Créditos expirados: {sp.Package?.Name ?? "pacote"} venceu em {BrasiliaTime.ToDate(sp.EndDate):dd/MM/yyyy}");

        var next = queue.Next;
        if (next is not null)
            await PromoteAsync(next, user, queue.IsPixPrepaid(next), "ativado da fila", ct);

        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return true;
    }

    // Regra 2 (21/10/2026 — DEMANDA_VALIDADE_FIM_DO_DIA_E_FILA_AO_ZERAR_CREDITOS_BACKEND.md): o
    // pacote da fila não espera mais a data do ativo — entra assim que os créditos do ativo
    // acabam. Rede de proteção do job: alunos com pacote ativo não recorrente e algo na fila. A
    // checagem de saldo/cotas/Pix fica toda em PromoteIfCreditsExhaustedAsync.
    public async Task<List<Guid>> ListStudentsWithQueueBehindActiveAsync(CancellationToken ct = default)
        => await db.StudentPackages
            .Where(sp => sp.Status == StudentPackageStatus.active
                && !sp.AutoRenew && sp.AsaasSubscriptionId == null)
            .Where(sp => db.StudentPackages.Any(q =>
                q.StudentId == sp.StudentId && q.Status == StudentPackageStatus.queued))
            .Select(sp => sp.StudentId)
            .Distinct()
            .ToListAsync(ct);

    public async Task<bool> PromoteIfCreditsExhaustedAsync(Guid studentId, CancellationToken ct = default)
    {
        await using var tx = await LockStudentAsync(studentId, ct);

        // Recorrente (AutoRenew, ou legado de assinatura Asaas) fica fora: o ciclo dele segue a
        // renovação, não o saldo.
        var sp = await db.StudentPackages
            .Include(x => x.Package)
            .Include(x => x.Allocations)
            .FirstOrDefaultAsync(x => x.StudentId == studentId
                && x.Status == StudentPackageStatus.active
                && !x.AutoRenew && x.AsaasSubscriptionId == null, ct);
        if (sp is null) return false;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == studentId, ct);
        if (user is null) return false;

        var queue = await LoadQueueAsync(studentId, ct);
        var next = queue.Next;
        if (next is null) return false;

        // Os créditos de um pacote da fila pago por Pix já entram no saldo na confirmação
        // (PaymentConfirmationService) — eles não são do ativo, então não contam como "sobrou".
        if (user.Credits - queue.PrepaidCredits > 0) return false;

        // Plano família: só está esgotado quando nenhum dependente tem cota sobrando. A cota do
        // titular é o próprio saldo (User.Credits), conferido acima.
        if (sp.Allocations.Any(a => a.DependentId != null && a.CreditsRemaining > 0)) return false;

        sp.Expire();
        ExpireCycleCredits(sp, user, keep: queue.PrepaidCredits,
            $"Créditos expirados: {sp.Package?.Name ?? "pacote"} encerrado (créditos esgotados)");

        await PromoteAsync(next, user, queue.IsPixPrepaid(next), "ativado da fila (créditos esgotados)", ct);

        db.AuditLogs.Add(AuditLog.Create(
            "package.queue_promoted_credits_exhausted", "StudentPackage", next.Id, studentId,
            $"Pacote ativo {sp.Package?.Name} ({sp.Id}, validade até " +
            $"{BrasiliaTime.ToDate(sp.EndDate):dd/MM/yyyy}) encerrado com os créditos esgotados; " +
            $"{next.Package?.Name} ({next.Id}) ativado da fila, válido até " +
            $"{BrasiliaTime.ToDate(next.EndDate):dd/MM/yyyy}."));

        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return true;
    }

    // Fila do aluno, mais antiga primeiro. Pacote da fila pago por Pix já confirmado já teve os
    // créditos somados ao saldo (PrepaidCredits); Pix ainda não confirmado não é promovido.
    private sealed record QueueState(
        StudentPackage? Next, int PrepaidCredits, Func<StudentPackage, bool> IsPixPrepaid);

    private async Task<QueueState> LoadQueueAsync(Guid studentId, CancellationToken ct)
    {
        var queue = await db.StudentPackages
            .Include(x => x.Package)
            .Where(x => x.StudentId == studentId && x.Status == StudentPackageStatus.queued)
            .OrderBy(x => x.PurchasedAt)
            .ToListAsync(ct);

        // Só compras feitas pelo App entram: o balcão (Channel "balcao", inclusive método "pix") nunca
        // credita pacote da fila na venda, então não pode ser tratado como pré-pago.
        var pixPurchases = queue.Count == 0
            ? []
            : await db.Purchases
                .Where(p => p.UserId == studentId && p.PaymentMethod == "pix" && p.Channel == "app")
                .ToListAsync(ct);

        // Pacote criado pelo carrinho do App (compra ligada por StudentPackageId) entra na fila com 0
        // crédito: o saldo só é creditado na promoção, então NÃO é pré-pago — tratá-lo como pré-pago
        // promovia sem creditar e a aluna perdia o que pagou. Só o fluxo antigo (compra sem ligação,
        // casada pelo PackageId) credita no webhook.
        bool IsCartLinked(StudentPackage q) => pixPurchases.Any(p => p.StudentPackageId == q.Id);
        bool IsPixPrepaid(StudentPackage q) =>
            !IsCartLinked(q) &&
            pixPurchases.Any(p => p.StudentPackageId == null && p.PackageId == q.PackageId && p.Status == "confirmed");
        bool IsPixPending(StudentPackage q) =>
            !IsCartLinked(q) && !IsPixPrepaid(q) &&
            pixPurchases.Any(p => p.StudentPackageId == null && p.PackageId == q.PackageId);

        return new QueueState(
            queue.FirstOrDefault(q => !IsPixPending(q) && q.Package is not null),
            queue.Where(IsPixPrepaid).Sum(q => q.Package?.GetCreditsToGrant() ?? 0),
            IsPixPrepaid);
    }

    // Serializa as viradas de pacote de um mesmo aluno: o débito (reserva) e o job podem tentar
    // promover a fila ao mesmo tempo, e sem a trava os dois creditariam o pacote novo. Trava a
    // linha do aluno em users (não um advisory lock): a reserva já segura essa linha desde o
    // UPDATE do saldo, então os dois caminhos travam na mesma ordem e não há deadlock. Usa a
    // transação já aberta por quem chamou (reserva, ajuste) ou abre uma própria — devolvida pra
    // quem chamou dar commit. Fora do Postgres (testes InMemory) não há trava.
    private async Task<IDbContextTransaction?> LockStudentAsync(Guid studentId, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return null;

        var own = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM users WHERE \"Id\" = {studentId} FOR UPDATE", ct);
        return own;
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
            $"Créditos expirados: {package.Name} — ciclo encerrado em {BrasiliaTime.ToDate(sp.EndDate):dd/MM/yyyy}");

        sp.ChargeRenewed(); // novo StartDate/EndDate, Status=active, zera tentativas e RenewalPaidForCycle

        await GrantCycleCreditsAsync(sp, package, user, creditsAlreadyInBalance: false,
            $"Pacote {package.Name} — novo ciclo (renovação automática)", "renewal_cycle", ct);
    }

    private async Task PromoteAsync(
        StudentPackage next, User? user, bool creditsAlreadyInBalance, string reason, CancellationToken ct)
    {
        var package = next.Package!;
        next.Activate();

        await GrantCycleCreditsAsync(next, package, user, creditsAlreadyInBalance,
            $"Pacote {package.Name} {reason}", "queue_activation", ct);
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
