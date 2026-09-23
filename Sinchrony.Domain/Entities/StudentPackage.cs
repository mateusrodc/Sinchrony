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
    // O ciclo seguinte já foi cobrado com sucesso, mas o EndDate atual ainda não chegou — job
    // cobra até 24h antes do vencimento (Termos 6.3: os créditos do ciclo atual só podem expirar
    // quando ele de fato terminar, não quando a cobrança confirma). Zera em ChargeRenewed().
    public bool RenewalPaidForCycle { get; private set; }

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
    // pro job tentar cobrar de novo já no próximo tick (em vez de esperar o prazo normal) com um
    // ciclo de tentativas limpo — o problema anterior era do cartão antigo.
    public void SetRenewalCard(Guid cardId)
    {
        RenewalCardId = cardId;
        if (PaymentStatus is SubscriptionPaymentStatus.retrying or SubscriptionPaymentStatus.overdue)
        {
            RenewalAttempts = 0;
            ProblemSince = null;
            NextRenewalAttemptAt = DateTime.UtcNow;
        }
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
    // job só tenha conseguido cobrar horas depois do vencimento. Reativa o Status: uma renovação
    // que ficou "pending" (antifraude) pode confirmar depois do pacote já ter expirado pela
    // varredura normal — sem isso o aluno pagava e ficava sem pacote mesmo assim.
    //
    // Exceção: se o EndDate anterior já ficou muito pra trás (aluno regularizou semanas depois
    // de overdue), encadear a partir dele geraria um EndDate novo ainda no passado — o
    // PackageExpirationService expiraria o pacote no tick seguinte e zeraria os créditos que
    // acabou de pagar. Nesse caso o ciclo novo começa agora mesmo, cheio.
    public void ChargeRenewed()
    {
        var previousEnd = EndDate;
        var now = DateTime.UtcNow;
        Status = StudentPackageStatus.active;
        StartDate = previousEnd > now ? previousEnd : now;
        EndDate = StartDate.AddDays((Package?.ValidityDays) ?? 30);
        RenewalAttempts = 0;
        NextRenewalAttemptAt = null;
        RenewalPaidForCycle = false;
    }

    // A cobrança do próximo ciclo confirmou antes do EndDate atual chegar (caso normal — o job
    // cobra até 24h antes do vencimento). Só marca que já está paga; a troca de créditos/cotas
    // (Termos 6.3) espera o EndDate de verdade — ver StudentPackageLifecycleService.TurnoverAsync.
    public void MarkRenewalPaidForCycle()
    {
        RenewalPaidForCycle = true;
        LastSyncedAt = DateTime.UtcNow;
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
    // cobrança vencida de fato. Zera NextRenewalAttemptAt: só SetRenewalCard() volta a preenchê-lo
    // — sem isso, o valor deixado por RecordRenewalFailure (ProblemSince + 3 dias) já está no
    // passado assim que essa mesma tentativa esgota, e o job cobraria de novo a cada tick pra
    // sempre (o pacote overdue nunca expira sozinho).
    public void MarkOverdue()
    {
        PaymentStatus = SubscriptionPaymentStatus.overdue;
        NextRenewalAttemptAt = null;
        LastSyncedAt = DateTime.UtcNow;
    }

    // Uma tentativa disparada pela troca de cartão num pacote já overdue também falhou — mantém
    // overdue sem reiniciar as 3 tentativas nem gerar outro alerta (o aluno já está bloqueado e
    // os admins já foram avisados). Só uma cobrança confirmada tira o pacote desse estado.
    public void KeepOverdueAfterRetryFailure(string? failureReason)
    {
        PaymentStatus = SubscriptionPaymentStatus.overdue;
        LastFailureReason = failureReason;
        NextRenewalAttemptAt = null;
        LastSyncedAt = DateTime.UtcNow;
    }
}
