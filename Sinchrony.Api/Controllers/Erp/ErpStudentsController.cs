using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.Filters;
using Sinchrony.Application.Common;
using Sinchrony.Application.Payments.Commands;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Domain.Services;
using Sinchrony.Infrastructure.Persistence.Repositories;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "admin,teacher")]
[ApiController]
[Route("api/students")]
[Produces("application/json")]
public class ErpStudentsController(
    IUserRepository userRepository,
    IPasswordService passwordService,
    IUnitContext unitContext,
    IStudentPackageRepository studentPackageRepository,
    IPackageRepository packageRepository,
    IDependentPackageAllocationRepository allocationRepository,
    IPurchaseRepository purchaseRepository,
    ICreditTransactionRepository creditTransactionRepository,
    PurchasePackageService purchasePackageService,
    IAuditService auditService,
    IUnitOfWork unitOfWork) : ControllerBase
{
    private static object MapStudent(User u, string? derivedPlan = null, string? paymentStatus = null) => new
    {
        id = u.Id,
        name = u.Name,
        email = u.Email,
        cpf = u.Cpf,
        phone = u.Phone,
        status = u.Status.ToString(),
        blockedReason = u.BlockedReason,
        paymentStatus,
        plan = derivedPlan ?? u.PlanName,
        credits = u.Credits,
        avatar = u.Avatar,
        unitId = u.UnitId,
        unitName = u.Unit?.Name,
        isDependent = u.IsDependent,
        responsibleStudentId = u.ResponsibleStudentId,
        registeredAt = u.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        lastVisit = (string?)null,
        totalClasses = 0,
        cep = u.Cep,
        logradouro = u.Logradouro,
        numero = u.Numero,
        complemento = u.Complemento,
        bairro = u.Bairro,
        cidade = u.Cidade,
        estado = u.Estado
    };

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? paymentStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (unitContext.IsGlobalAdmin || !unitContext.UnitId.HasValue)
        {
            var (items, total) = await userRepository.ListStudentsPagedAsync(
                status, page, pageSize, ct, paymentStatus);
            var mapped = await MapStudentsWithPaymentStatusAsync(items, ct);
            return Ok(PagedResult.Create(mapped, page, pageSize, total));
        }
        else
        {
            var all = await userRepository.ListStudentsByUnitAsync(unitContext.UnitId.Value, ct);
            if (!string.IsNullOrEmpty(status))
                all = all.Where(u => u.Status.ToString() == status);

            if (!string.IsNullOrEmpty(paymentStatus))
            {
                var allIds = all.Select(u => u.Id).ToList();
                var subs = await studentPackageRepository.ListSubscriptionsByStudentIdsAsync(allIds, ct);
                var matchingStudentIds = subs
                    .Where(sp => sp.PaymentStatus?.ToString() == paymentStatus)
                    .Select(sp => sp.StudentId).ToHashSet();
                all = all.Where(u => matchingStudentIds.Contains(u.Id));
            }

            var list = all.ToList();
            var total = list.Count;
            var items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var mapped = await MapStudentsWithPaymentStatusAsync(items, ct);
            return Ok(PagedResult.Create(mapped, page, pageSize, total));
        }
    }

    // Batched — evita uma query de StudentPackage por aluno da página.
    private async Task<IEnumerable<object>> MapStudentsWithPaymentStatusAsync(
        IEnumerable<User> items, CancellationToken ct)
    {
        var list = items.ToList();
        var subs = await studentPackageRepository.ListSubscriptionsByStudentIdsAsync(
            list.Select(u => u.Id), ct);
        var byStudent = subs.ToDictionary(sp => sp.StudentId, sp => sp.PaymentStatus?.ToString());

        return list.Select(u => MapStudent(u, paymentStatus: byStudent.GetValueOrDefault(u.Id)));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var student = await userRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Student not found.");

        if (!unitContext.IsGlobalAdmin && unitContext.UnitId.HasValue
            && student.UnitId != unitContext.UnitId)
            return Forbid();

        // Deriva o plano do StudentPackage ativo
        var activePackage = await studentPackageRepository.GetActiveByStudentAsync(id, ct);
        var derivedPlan = activePackage?.Package?.PackageType?.Name ?? student.PlanName;

        return Ok(MapStudent(student, derivedPlan));
    }

    [HttpGet("{id}/history")]
    public async Task<IActionResult> History(Guid id, CancellationToken ct)
    {
        var student = await userRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Student not found.");

        if (!unitContext.IsGlobalAdmin && unitContext.UnitId.HasValue
            && student.UnitId != unitContext.UnitId)
            return Forbid();

        return Ok(new { data = new List<object>() });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStudentRequest req, CancellationToken ct)
    {
        var existing = await userRepository.GetByEmailAsync(req.email, ct);
        if (existing is not null)
            throw DomainException.Conflict("EMAIL_IN_USE", "Email already in use.");

        if (!string.IsNullOrEmpty(req.cpf) && !CpfValidator.IsValid(req.cpf))
            throw DomainException.Validation("INVALID_CPF", "CPF inválido.");

        var hash = passwordService.HashPassword(Guid.NewGuid().ToString());
        var student = Domain.Entities.User.Create(req.name, req.email, req.phone, hash, Role.student,
            string.IsNullOrEmpty(req.cpf) ? null : CpfValidator.Sanitize(req.cpf));

        if (!string.IsNullOrEmpty(req.cpf))
        {
            var cpfSanitized = CpfValidator.Sanitize(req.cpf);
            var cpfInUse = await userRepository.GetByCpfAsync(cpfSanitized, ct);
            if (cpfInUse is not null)
                throw DomainException.Conflict("CPF_ALREADY_IN_USE", "CPF já cadastrado.");
        }

        if (!string.IsNullOrEmpty(req.plan))
            student.UpdatePlan(req.plan);

        student.UpdateAddress(req.cep, req.logradouro, req.numero,
            req.complemento, req.bairro, req.cidade, req.estado);

        // Vincula à unidade do admin ou à unidade informada
        var unitId = req.unitId ?? unitContext.UnitId;
        if (unitId.HasValue)
            student.SetUnit(unitId.Value);

        await userRepository.AddAsync(student, ct);
        await userRepository.SaveAsync(ct);

        await auditService.LogAsync(
            "student.created", "User", student.Id, AdminId,
            $"Name: {student.Name}, Email: {student.Email}", ct: ct);

        return StatusCode(201, MapStudent(student));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStudentRequest req, CancellationToken ct)
    {
        var student = await userRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Student not found.");

        if (!unitContext.IsGlobalAdmin && unitContext.UnitId.HasValue
            && student.UnitId != unitContext.UnitId)
            return Forbid();

        student.UpdateProfile(req.name, req.email, req.phone, student.Avatar);

        if (!string.IsNullOrEmpty(req.cpf))
        {
            if (!CpfValidator.IsValid(req.cpf))
                throw DomainException.Validation("INVALID_CPF", "CPF inválido.");
            student.UpdateCpf(req.cpf);
        }

        if (!string.IsNullOrEmpty(req.cpf))
        {
            var cpfSanitized = CpfValidator.Sanitize(req.cpf);
            var cpfInUse = await userRepository.GetByCpfAsync(cpfSanitized, ct);
            if (cpfInUse is not null && cpfInUse.Id != id)
                throw DomainException.Conflict("CPF_ALREADY_IN_USE", "CPF já cadastrado.");
        }

        if (!string.IsNullOrEmpty(req.status))
        {
            switch (req.status)
            {
                case "active": student.Reactivate(); break;
                case "inactive": student.Deactivate(); break;
                case "blocked": student.Block(); break;
            }
        }

        if (req.plan is not null) student.UpdatePlan(req.plan);

        student.UpdateAddress(req.cep, req.logradouro, req.numero,
            req.complemento, req.bairro, req.cidade, req.estado);

        if (req.unitId.HasValue)
            student.SetUnit(req.unitId.Value);

        await userRepository.SaveAsync(ct);

        await auditService.LogAsync(
            "student.updated", "User", student.Id, AdminId,
            $"Name: {student.Name}, Email: {student.Email}", ct: ct);

        return Ok(MapStudent(student));
    }
    [HttpPatch("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var student = await userRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Student not found.");

        student.Deactivate();
        await userRepository.SaveAsync(ct);

        await auditService.LogAsync("student.deactivated", "User", id, AdminId, ct: ct);

        return Ok(new { success = true, status = "inactive" });
    }

    [HttpPatch("{id}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
    {
        var student = await userRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Student not found.");

        student.Reactivate();
        await userRepository.SaveAsync(ct);

        await auditService.LogAsync("student.reactivated", "User", id, AdminId, ct: ct);

        return Ok(new { success = true, status = "active" });
    }
    private Guid AdminId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
    ?? User.FindFirstValue("sub")!);

    [HttpPost("{studentId}/packages")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AssignPackage(
        Guid studentId,
        [FromBody] AssignPackageRequest req,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.reason) || req.reason.Trim().Length < 3)
            throw DomainException.Validation("REASON_REQUIRED",
                "O motivo da concessão é obrigatório (mínimo 3 caracteres).");

        if (req.paymentMethod != "cash" && req.paymentMethod != "courtesy")
            throw DomainException.Validation("INVALID_PAYMENT_METHOD",
                "Método de pagamento inválido. Use 'cash' ou 'courtesy'.");

        if (req.paymentMethod == "cash" && (req.amount == null || req.amount <= 0))
            throw DomainException.Validation("AMOUNT_REQUIRED",
                "O valor é obrigatório para pagamento em dinheiro.");

        var student = await userRepository.GetByIdAsync(studentId, ct)
            ?? throw DomainException.NotFound("Student not found.");

        var package = await packageRepository.GetByIdAsync(req.packageId, ct)
            ?? throw DomainException.NotFound("Package not found.");

        if (!package.Active)
            throw DomainException.Validation("PACKAGE_INACTIVE", "Package is not available.");

        if (!unitContext.IsGlobalAdmin && unitContext.UnitId.HasValue)
        {
            if (package.UnitId.HasValue && package.UnitId != unitContext.UnitId)
                throw DomainException.Forbidden("Você não tem permissão para conceder pacotes de outra unidade.");
        }

        var amount = req.paymentMethod == "courtesy" ? 0 : (req.amount ?? 0);

        var purchase = Purchase.CreateConfirmed(
            studentId, package.Id, amount,
            req.paymentMethod, null);
        await purchaseRepository.AddAsync(purchase, ct);
        await purchaseRepository.SaveAsync(ct);

        var grant = await purchasePackageService.ProcessAsync(studentId, package, "manual", ct);

        // Recarrega student para pegar Credits já creditados pelo ProcessAsync
        student = await userRepository.GetByIdAsync(studentId, ct)!;

        // Só registra no extrato o que realmente entrou no saldo. Pacote que foi pra fila (ou só
        // estendeu validade) não credita nada agora — antes o extrato mostrava +N com saldo inalterado.
        if (grant.CreditsAdded > 0)
        {
            var creditTx = CreditTransaction.Create(
                studentId,
                grant.CreditsAdded,
                student!.Credits,
                $"Concessão manual ({req.paymentMethod}): {package.Name} — {req.reason}",
                "manual",
                purchase.Id);
            await creditTransactionRepository.AddAsync(creditTx, ct);
            await creditTransactionRepository.SaveAsync(ct);
        }

        await auditService.LogAsync(
            "package.granted_by_admin", "Purchase",
            purchase.Id, AdminId,
            $"Aluno: {student.Name}, Pacote: {package.Name}, Método: {req.paymentMethod}, Motivo: {req.reason}",
            ct: ct);

        // Pacote tratado por esta concessão — pode ser um pacote em fila, diferente do ativo.
        var sp = grant.StudentPackage;

        return StatusCode(201, new
        {
            id = sp?.Id,
            packageId = package.Id,
            packageName = package.Name,
            status = sp?.Status.ToString() ?? "queued",
            startDate = sp?.StartDate,
            endDate = sp?.EndDate,
            transactionId = (string?)null,
            paymentMethod = req.paymentMethod
        });
    }
    [HttpDelete("{studentId}/packages/{studentPackageId}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> RemovePackage(
    Guid studentId,
    Guid studentPackageId,
    [FromBody] RemovePackageRequest req,
    CancellationToken ct)
    {
        // Validação de reason
        if (string.IsNullOrWhiteSpace(req.reason) || req.reason.Trim().Length < 3)
            throw DomainException.Validation("REASON_REQUIRED",
                "O motivo da remoção é obrigatório (mínimo 3 caracteres).");

        // Busca StudentPackage
        var sp = await studentPackageRepository.GetByIdAsync(studentPackageId, ct);
        if (sp is null || sp.StudentId != studentId)
            throw DomainException.NotFound("StudentPackage not found.");

        // Valida origem manual
        if (sp.Source != "manual")
            throw DomainException.Validation("NOT_MANUAL_GRANT",
                "Apenas pacotes concedidos manualmente pela ERP podem ser removidos.");

        // Valida status
        if (sp.Status == StudentPackageStatus.cancelled)
            throw DomainException.Conflict("PACKAGE_ALREADY_CANCELLED",
                "Este pacote já está cancelado.");

        // Multiunidade
        var package = await packageRepository.GetByIdAsync(sp.PackageId, ct);
        if (!unitContext.IsGlobalAdmin && unitContext.UnitId.HasValue)
        {
            if (package?.UnitId.HasValue == true && package.UnitId != unitContext.UnitId)
                throw DomainException.Forbidden("Você não tem permissão para remover pacotes de outra unidade.");
        }

        // Carrega aluno
        var student = await userRepository.GetByIdAsync(studentId, ct)
            ?? throw DomainException.NotFound("Student not found.");

        // Checagem de uso — não permite estorno se créditos já foram usados
        if (student.Credits < sp.CreditsGranted)
            throw DomainException.Conflict("CREDITS_ALREADY_USED",
                $"O aluno já utilizou créditos deste pacote. Saldo atual ({student.Credits}) é menor que os créditos concedidos ({sp.CreditsGranted}).");

        // Estorna créditos
        student.DeductCredits(sp.CreditsGranted);

        // CreditTransaction negativa
        var creditTx = CreditTransaction.Create(
            studentId,
            -sp.CreditsGranted,
            student.Credits,
            $"Remoção de concessão manual: {package?.Name ?? sp.PackageId.ToString()} — {req.reason}",
            "manual_reversal",
            studentPackageId);
        await creditTransactionRepository.AddAsync(creditTx, ct);
        await creditTransactionRepository.SaveAsync(ct);

        // Cancela o pacote
        sp.Cancel();

        // Zera allocations se existirem
        foreach (var alloc in sp.Allocations)
            alloc.Debit(alloc.CreditsRemaining);

        await studentPackageRepository.SaveAsync(ct);
        await userRepository.SaveAsync(ct);

        // Auditoria
        await auditService.LogAsync(
            "package.grant_removed_by_admin", "StudentPackage",
            sp.Id, AdminId,
            $"Aluno: {student.Name}, Pacote: {package?.Name}, Créditos estornados: {sp.CreditsGranted}, Motivo: {req.reason}",
            ct: ct);

        return Ok(new
        {
            id = sp.Id,
            packageId = sp.PackageId,
            packageName = package?.Name,
            status = "cancelled",
            creditsReverted = sp.CreditsGranted
        });
    }

    // Fase 1 — DEMANDA_CONTROLE_ADMIN_PERMISSOES_BACKEND.md. Motivado por um incidente real:
    // AssignPackage/RemovePackage são modelados em torno de pacotes, e RemovePackage se recusa
    // a estornar quando o aluno já usou parte do crédito (CREDITS_ALREADY_USED) — não existia
    // nenhuma via administrativa pra corrigir um saldo de crédito diretamente, só SQL direto
    // no banco de produção. Este endpoint existe pra aposentar essa via.
    [HttpPost("{id}/credits/adjust")]
    [Authorize(Roles = "admin")]
    [RequirePermission("credit", "edit")]
    public async Task<IActionResult> AdjustCredits(
        Guid id,
        [FromBody] AdjustCreditsRequest req,
        CancellationToken ct)
    {
        var student = await userRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Student not found.");

        if (student.Role != Role.student)
            throw DomainException.Validation("NOT_A_STUDENT",
                "Só é possível ajustar créditos de uma conta de aluno.");

        // Mesmo isolamento multi-unidade já aplicado em Get/Update deste controller — sem
        // isso, um admin de uma unidade poderia ajustar crédito de aluno de outra unidade.
        if (!unitContext.IsGlobalAdmin && unitContext.UnitId.HasValue
            && student.UnitId != unitContext.UnitId)
            return Forbid();

        if (req.extendDays is <= 0)
            throw DomainException.Validation("INVALID_DAYS",
                "O número de dias de extensão deve ser maior que zero.");

        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // Os créditos expiram junto com o pacote ativo. Quando o ajuste é um crédito "de hoje"
            // (ex.: reposição), extendDays empurra a validade desse pacote pra que o saldo
            // ajustado não expire junto com uma vigência antiga.
            StudentPackage? extendedPackage = null;
            if (req.extendDays.HasValue)
            {
                extendedPackage = await studentPackageRepository.GetActiveByStudentAsync(id, ct)
                    ?? throw DomainException.Validation("NO_ACTIVE_PACKAGE",
                        "O aluno não tem pacote ativo para estender a validade.");
                extendedPackage.ExtendValidity(req.extendDays.Value);
            }

            // AdjustCredits (domínio) já valida reason vazio e saldo negativo antes de mutar
            // o estado — ver ck_users_credits em UserConfiguration.
            student.AdjustCredits(req.delta, req.reason);
            await userRepository.SaveAsync(ct);
            if (extendedPackage is not null)
                await studentPackageRepository.SaveAsync(ct);

            // Type "manual_adjustment", distinto de "manual" (concessão de pacote) — pro
            // extrato (GET .../credit-transactions) conseguir diferenciar as duas operações.
            var creditTx = CreditTransaction.Create(
                student.Id, req.delta, student.Credits,
                $"Ajuste manual: {req.reason}", "manual_adjustment", null);
            await creditTransactionRepository.AddAsync(creditTx, ct);
            await creditTransactionRepository.SaveAsync(ct);

            await auditService.LogAsync(
                "credit.manually_adjusted", "User",
                student.Id, AdminId,
                $"Delta: {req.delta}, Reason: {req.reason}, NewBalance: {student.Credits}" +
                (extendedPackage is null
                    ? ""
                    : $", ExtendDays: {req.extendDays}, NewEndDate: {extendedPackage.EndDate:yyyy-MM-dd}"),
                ct: ct);

            await unitOfWork.CommitAsync(ct);

            return Ok(new
            {
                credits = student.Credits,
                packageEndDate = extendedPackage?.EndDate
            });
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }
    }

    // Pré-requisito pro ERP mostrar o extrato antes de ajustar. ICreditTransactionRepository.
    // ListByUserAsync já existia no repositório, mas nenhum controller o expunha até agora.
    [HttpGet("{id}/credit-transactions")]
    [Authorize(Roles = "admin,teacher")]
    public async Task<IActionResult> ListCreditTransactions(Guid id, CancellationToken ct)
    {
        var student = await userRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Student not found.");

        if (!unitContext.IsGlobalAdmin && unitContext.UnitId.HasValue
            && student.UnitId != unitContext.UnitId)
            return Forbid();

        var transactions = await creditTransactionRepository.ListByUserAsync(id, ct);
        return Ok(new
        {
            data = transactions.Select(t => new
            {
                id = t.Id,
                amount = t.Amount,
                balanceAfter = t.BalanceAfter,
                reason = t.Reason,
                type = t.Type,
                createdAt = t.CreatedAt
            })
        });
    }

    public record AdjustCreditsRequest(int delta, string reason, int? extendDays = null);

    public record AssignPackageRequest(
        Guid packageId,
        string paymentMethod,
        decimal? amount,
        string reason);
}

public record RemovePackageRequest(string reason);

public record CreateStudentRequest(
    string name, string email, string? phone,
    string? plan, string? status, string? cpf,
    string? cep, string? logradouro, string? numero,
    string? complemento, string? bairro, string? cidade, string? estado,
    Guid? unitId = null);

public record UpdateStudentRequest(
    string name, string email, string? phone,
    string? status, string? plan, string? cpf,
    string? cep, string? logradouro, string? numero,
    string? complemento, string? bairro, string? cidade, string? estado,
    Guid? unitId = null);