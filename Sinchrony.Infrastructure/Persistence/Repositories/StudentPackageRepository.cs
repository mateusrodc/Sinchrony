using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class StudentPackageRepository(ApplicationDbContext db) : IStudentPackageRepository
{
    public async Task<StudentPackage?> GetActiveByStudentAsync(Guid studentId, CancellationToken ct = default)
        => await db.StudentPackages
            .Include(sp => sp.Package).ThenInclude(p => p!.PackageType)
            .Include(sp => sp.Allocations)
            .FirstOrDefaultAsync(sp =>
                sp.StudentId == studentId &&
                sp.Status == StudentPackageStatus.active, ct);

    public async Task<StudentPackage?> GetQueuedByStudentAsync(Guid studentId, CancellationToken ct = default)
        => await db.StudentPackages
            .Include(sp => sp.Package)
            .FirstOrDefaultAsync(sp =>
                sp.StudentId == studentId &&
                sp.Status == StudentPackageStatus.queued, ct);

    public async Task<IEnumerable<StudentPackage>> ListByStudentAsync(Guid studentId, CancellationToken ct = default)
        => await db.StudentPackages
            .Include(sp => sp.Package).ThenInclude(p => p!.PackageType)
            .Include(sp => sp.Allocations)
            .Where(sp => sp.StudentId == studentId)
            .OrderByDescending(sp => sp.PurchasedAt)
            .ToListAsync(ct);

    public async Task AddAsync(StudentPackage studentPackage, CancellationToken ct = default)
        => await db.StudentPackages.AddAsync(studentPackage, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);

    public async Task<StudentPackage?> GetByIdAsync(Guid id, CancellationToken ct = default)
    => await db.StudentPackages
        .Include(sp => sp.Package).ThenInclude(p => p!.PackageType)
        .Include(sp => sp.Allocations)
        .FirstOrDefaultAsync(sp => sp.Id == id, ct);

    public async Task<StudentPackage?> GetByAsaasSubscriptionIdAsync(string subscriptionId, CancellationToken ct = default)
        => await db.StudentPackages
            .Include(sp => sp.Package).ThenInclude(p => p!.PackageType)
            .Where(sp => sp.AsaasSubscriptionId == subscriptionId)
            .OrderByDescending(sp => sp.PurchasedAt)
            .FirstOrDefaultAsync(ct);

    // "É/foi recorrente" é julgado por PaymentStatus != null (setado uma vez em EnableAutoRenew e
    // nunca voltado a null depois — CancelRenewal/Cancel só o levam pra "cancelled") em vez de
    // AutoRenew sozinho: AutoRenew vira false ao cancelar a renovação, e se ele fosse o critério
    // o pacote sumiria de todas as telas de status junto (inclusive do próprio histórico do
    // aluno) assim que ele cancelasse. AutoRenew continua sendo o que decide se o job cobra.
    public async Task<StudentPackage?> GetSubscriptionByStudentAsync(Guid studentId, CancellationToken ct = default)
    {
        var active = await db.StudentPackages
            .Include(sp => sp.Package).ThenInclude(p => p!.PackageType)
            .FirstOrDefaultAsync(sp =>
                sp.StudentId == studentId &&
                sp.Status == StudentPackageStatus.active &&
                (sp.PaymentStatus != null || sp.AsaasSubscriptionId != null), ct);
        if (active is not null) return active;

        var queued = await db.StudentPackages
            .Include(sp => sp.Package).ThenInclude(p => p!.PackageType)
            .FirstOrDefaultAsync(sp =>
                sp.StudentId == studentId &&
                sp.Status == StudentPackageStatus.queued &&
                (sp.PaymentStatus != null || sp.AsaasSubscriptionId != null), ct);
        if (queued is not null) return queued;

        // Sem pacote ativo nem na fila (cancelado ou expirado) — ainda assim devolve o último
        // recorrente, pra o aluno continuar vendo o histórico de cobranças depois de cancelar.
        return await db.StudentPackages
            .Include(sp => sp.Package).ThenInclude(p => p!.PackageType)
            .Where(sp => sp.StudentId == studentId &&
                (sp.PaymentStatus != null || sp.AsaasSubscriptionId != null))
            .OrderByDescending(sp => sp.PurchasedAt)
            .FirstOrDefaultAsync(ct);
    }

    // Elegível pro job cobrar agora. Quando NextRenewalAttemptAt está preenchido, só ele decide
    // (é uma retentativa agendada, ou o aluno acabou de trocar o cartão) — sem essa exclusividade,
    // uma retentativa agendada pra daqui a 1/3 dias seria ignorada e o pacote seria selecionado de
    // novo no próximo tick só por estar dentro da janela de 24h do vencimento. Sem
    // NextRenewalAttemptAt, o gatilho é só a janela de 24h — e nunca pra quem já está overdue
    // (só volta a ser tentado explicitamente via troca de cartão).
    public async Task<List<Guid>> ListDueForRenewalAsync(DateTime now, CancellationToken ct = default)
    {
        var windowStart = now.AddHours(24);
        return await db.StudentPackages
            .Where(sp => sp.AutoRenew && sp.Status == StudentPackageStatus.active)
            // Ciclo seguinte já pago, só esperando o EndDate virar (Termos 6.3) — nada a cobrar.
            .Where(sp => !sp.RenewalPaidForCycle)
            .Where(sp =>
                (sp.PaymentStatus != SubscriptionPaymentStatus.overdue
                    && ((sp.NextRenewalAttemptAt == null && sp.EndDate <= windowStart)
                        || sp.NextRenewalAttemptAt <= now))
                || (sp.PaymentStatus == SubscriptionPaymentStatus.overdue && sp.NextRenewalAttemptAt <= now))
            .Select(sp => sp.Id)
            .ToListAsync(ct);
    }

    public async Task<SubscriptionListPage> ListSubscriptionsPagedAsync(
        SubscriptionListFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        var baseQuery = db.StudentPackages.AsNoTracking()
            .Where(sp => sp.PaymentStatus != null || sp.AsaasSubscriptionId != null);

        if (filter.UnitId.HasValue)
            baseQuery = baseQuery.Where(sp => sp.Student!.UnitId == filter.UnitId.Value);
        if (filter.DueFrom.HasValue)
        {
            // nextDueDate = EndDate em horário de Brasília (UTC-3) — desloca o filtro em vez do
            // EndDate inteiro pra poder comparar com DateOnly diretamente.
            var fromUtc = filter.DueFrom.Value.ToDateTime(TimeOnly.MinValue).AddHours(3);
            baseQuery = baseQuery.Where(sp => sp.EndDate >= fromUtc);
        }
        if (filter.DueTo.HasValue)
        {
            var toExclusiveUtc = filter.DueTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(3);
            baseQuery = baseQuery.Where(sp => sp.EndDate < toExclusiveUtc);
        }
        if (!string.IsNullOrWhiteSpace(filter.StudentSearch))
        {
            var term = filter.StudentSearch.Trim()
                .Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            var pattern = $"%{term}%";
            baseQuery = baseQuery.Where(sp =>
                EF.Functions.ILike(sp.Student!.Name, pattern) ||
                EF.Functions.ILike(sp.Student.Email, pattern));
        }

        // Summary considera todos os filtros exceto Status, pra os cards continuarem
        // mostrando a contagem total do recorte (mesmo padrão do ErpPurchasesController).
        var summaryCounts = await baseQuery
            .GroupBy(sp => sp.PaymentStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // amount = preço atual do pacote (não guardamos mais um valor "próxima cobrança"
        // separado — é sempre Package.Price, ver PurchasePackageCommand/RecurringRenewalService).
        var expectedMonthlyRevenue = await baseQuery
            .Where(sp => sp.PaymentStatus != SubscriptionPaymentStatus.cancelled)
            .SumAsync(sp => (decimal?)sp.Package!.Price, ct) ?? 0m;

        var overdueAmount = await baseQuery
            .Where(sp => sp.PaymentStatus == SubscriptionPaymentStatus.overdue)
            .SumAsync(sp => (decimal?)sp.Package!.Price, ct) ?? 0m;

        int CountOf(SubscriptionPaymentStatus status) =>
            summaryCounts.FirstOrDefault(c => c.Status == status)?.Count ?? 0;

        var summary = new SubscriptionSummary(
            CountOf(SubscriptionPaymentStatus.up_to_date),
            CountOf(SubscriptionPaymentStatus.retrying),
            CountOf(SubscriptionPaymentStatus.overdue),
            CountOf(SubscriptionPaymentStatus.cancelled),
            expectedMonthlyRevenue, overdueAmount);

        var query = baseQuery;
        if (!string.IsNullOrEmpty(filter.Status) &&
            Enum.TryParse<SubscriptionPaymentStatus>(filter.Status, out var statusFilter))
            query = query.Where(sp => sp.PaymentStatus == statusFilter);

        var total = await query.CountAsync(ct);
        var items = await query
            .Include(sp => sp.Package)
            .Include(sp => sp.Student)
            .OrderBy(sp => sp.EndDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new SubscriptionListPage(items, total, summary);
    }

    public async Task<IEnumerable<StudentPackage>> ListSubscriptionsByStudentIdsAsync(
        IEnumerable<Guid> studentIds, CancellationToken ct = default)
        => await db.StudentPackages
            .Where(sp => studentIds.Contains(sp.StudentId)
                && (sp.PaymentStatus != null || sp.AsaasSubscriptionId != null)
                && sp.Status != StudentPackageStatus.cancelled)
            .ToListAsync(ct);
}