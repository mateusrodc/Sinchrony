namespace Sinchrony.Domain.Entities;

public enum StudentPackageStatus { active, queued, expired, cancelled }

// Estado de cobrança de um pacote recorrente (AutoRenew == true, ou legado AsaasSubscriptionId
// != null). Independente de StudentPackageStatus: um pacote pode estar "active" (dentro da
// vigência do ciclo pago) e "retrying" (a renovação do próximo ciclo ainda não foi confirmada)
// ao mesmo tempo.
public enum SubscriptionPaymentStatus { up_to_date, retrying, overdue, cancelled }

public class StudentPackage
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid StudentId { get; private set; }
    public Guid PackageId { get; private set; }
    public StudentPackageStatus Status { get; private set; } = StudentPackageStatus.active;
    public DateTime PurchasedAt { get; private set; } = DateTime.UtcNow;
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }

    public User? Student { get; private set; }
    public Package? Package { get; private set; }
    public ICollection<DependentPackageAllocation> Allocations { get; private set; } = [];

    public string Source { get; private set; } = "purchase"; // "purchase" | "manual"
    public int CreditsGranted { get; private set; }

    // Legado: id da assinatura na Asaas, de quando a recorrência era cobrada via
    // POST /v3/subscriptions (modelo abandonado em 23/09 — ver DEMANDA_RECORRENCIA...V2.md).
    // Mantido só como rede de segurança pra um eventual registro antigo; contratações novas não
    // preenchem mais este campo.
    public string? AsaasSubscriptionId { get; private set; }

    // Renovação automática cobrada pelo próprio Sinchrony (ChargeCardAsync no cartão salvo, no
    // vencimento do ValidityDays do pacote) — modelo atual pra Package.IsRecurring.
    public bool AutoRenew { get; private set; }
    public Guid? RenewalCardId { get; private set; }
    // Tentativas de cobrança do ciclo atual; zera ao confirmar ou ao trocar o cartão.
    public int RenewalAttempts { get; private set; }
    public DateTime? NextRenewalAttemptAt { get; private set; }

    // Estado de cobrança — preenchido quando AutoRenew == true (ou, no legado, quando
    // AsaasSubscriptionId != null). Mantido pelo RecurringRenewalService (job + webhook + sync).
    public SubscriptionPaymentStatus? PaymentStatus { get; private set; }
    public DateTime? LastPaidAt { get; private set; }
    public decimal? LastPaidAmount { get; private set; }
    // Primeira recusa do ciclo de cobrança atual (limpa quando o ciclo confirma).
    public DateTime? ProblemSince { get; private set; }
    public string? LastFailureReason { get; private set; }
    public DateTime? LastSyncedAt { get; private set; }

    protected StudentPackage() { }

    public static StudentPackage Create(Guid studentId, Guid packageId, int validityDays)
    {
        var start = DateTime.UtcNow;
        return new StudentPackage
        {
            StudentId = studentId,
            PackageId = packageId,
            StartDate = start,
            EndDate = start.AddDays(validityDays),
            Status = StudentPackageStatus.active
        };
    }
    public void SetSource(string source, int creditsGranted)
    {
        Source = source;
        CreditsGranted = creditsGranted;
    }

    public static StudentPackage CreateQueued(Guid studentId, Guid packageId, int validityDays)
    {
        var sp = Create(studentId, packageId, validityDays);
        sp.Status = StudentPackageStatus.queued;
        return sp;
    }

    public bool IsExpired() => EndDate < DateTime.UtcNow;

    public void Expire()
    {
        Status = StudentPackageStatus.expired;
    }

    // Cancelamento total (admin: cancel-refund, remoção de pacote manual). Cobre tanto o modelo
    // atual (AutoRenew) quanto o legado (AsaasSubscriptionId) num só lugar.
    public void Cancel()
    {
        Status = StudentPackageStatus.cancelled;
        if (AutoRenew || AsaasSubscriptionId is not null)
        {
            AutoRenew = false;
            PaymentStatus = SubscriptionPaymentStatus.cancelled;
            LastSyncedAt = DateTime.UtcNow;
        }
    }

    // Aluno desiste só da renovação — o pacote continua válido normalmente até o EndDate atual,
    // simplesmente não gera um próximo ciclo.
    public void CancelRenewal()
    {
        AutoRenew = false;
        PaymentStatus = SubscriptionPaymentStatus.cancelled;
        LastSyncedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        Status = StudentPackageStatus.active;
        StartDate = DateTime.UtcNow;
        EndDate = StartDate.AddDays((Package?.ValidityDays) ?? 30);
    }
    public void ExtendValidity(int days)
    {
        EndDate = EndDate.AddDays(days);
    }

    public void SetAsaasSubscriptionId(string subscriptionId)
    {
        AsaasSubscriptionId = subscriptionId;
    }

    // Contratação de um pacote recorrente: liga a renovação automática cobrada pelo Sinchrony.
    public void EnableAutoRenew(Guid? cardId)
    {
        AutoRenew = true;
        RenewalCardId = cardId;
        PaymentStatus = SubscriptionPaymentStatus.up_to_date;
        LastSyncedAt = DateTime.UtcNow;
    }

    // Troca do cartão usado nas renovações. Se o pacote estava em retentativa/vencido, sinaliza
    // pro job tentar cobrar de novo já no próximo tick, em vez de esperar o prazo normal.
    public void SetRenewalCard(Guid cardId)
    {
        RenewalCardId = cardId;
        if (PaymentStatus is SubscriptionPaymentStatus.retrying or SubscriptionPaymentStatus.overdue)
            NextRenewalAttemptAt = DateTime.UtcNow;
    }

    // Legado (assinatura Asaas): reinicia a vigência a partir de agora. Não usar pro modelo
    // atual — ver ChargeRenewed(), que encadeia a partir do EndDate anterior.
    public void RenewCycle()
    {
        Status = StudentPackageStatus.active;
        StartDate = DateTime.UtcNow;
        EndDate = StartDate.AddDays((Package?.ValidityDays) ?? 30);
    }

    // Renovação cobrada com sucesso: o próximo ciclo começa exatamente onde o anterior terminou
    // (não em "agora") — um pacote de 90 dias continua valendo 90 dias por ciclo, mesmo que o
    // job só tenha conseguido cobrar horas depois do vencimento.
    public void ChargeRenewed()
    {
        var previousEnd = EndDate;
        StartDate = previousEnd;
        EndDate = previousEnd.AddDays((Package?.ValidityDays) ?? 30);
        RenewalAttempts = 0;
        NextRenewalAttemptAt = null;
    }

    // Ciclo atual pago (renovação automática ou legado de assinatura Asaas). Limpa o problema em
    // aberto, se havia.
    public void MarkUpToDate(DateTime paidAt, decimal paidAmount)
    {
        PaymentStatus = SubscriptionPaymentStatus.up_to_date;
        LastPaidAt = paidAt;
        LastPaidAmount = paidAmount;
        ProblemSince = null;
        LastFailureReason = null;
        LastSyncedAt = DateTime.UtcNow;
    }

    // Uma tentativa de renovação falhou, mas ainda há tentativas restantes (não é a 3ª/última).
    // ProblemSince ancora as retentativas (+1 dia, +3 dias a partir da 1ª falha do ciclo) — só é
    // setado na primeira falha, uma segunda recusa não o reinicia.
    public void RecordRenewalFailure(string? failureReason)
    {
        RenewalAttempts++;
        ProblemSince ??= DateTime.UtcNow;
        PaymentStatus = SubscriptionPaymentStatus.retrying;
        LastFailureReason = failureReason;
        NextRenewalAttemptAt = RenewalAttempts switch
        {
            1 => ProblemSince.Value.AddDays(1),
            _ => ProblemSince.Value.AddDays(3)
        };
        LastSyncedAt = DateTime.UtcNow;
    }

    // Legado (webhook de assinatura Asaas): recusa avisada preventivamente, sem contar como
    // tentativa de renovação nossa.
    public void MarkRetrying(string? failureReason)
    {
        PaymentStatus = SubscriptionPaymentStatus.retrying;
        ProblemSince ??= DateTime.UtcNow;
        LastFailureReason = failureReason;
        LastSyncedAt = DateTime.UtcNow;
    }

    // As tentativas de renovação se esgotaram (ou, no legado, a Asaas desistiu de reprocessar) —
    // cobrança vencida de fato.
    public void MarkOverdue()
    {
        PaymentStatus = SubscriptionPaymentStatus.overdue;
        LastSyncedAt = DateTime.UtcNow;
    }
}
