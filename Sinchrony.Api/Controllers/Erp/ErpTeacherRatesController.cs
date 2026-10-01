using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.Erp;

// Valor da aula (por modalidade, com um padrão) e bônus por aluno presente, com histórico por data
// de vigência. Mudança de preço = registro NOVO com effectiveFrom; nunca edita o antigo, para o
// relatório de um mês passado continuar saindo com os valores que valiam naquele mês.
[Authorize(Roles = "admin")]
[ApiController]
[Produces("application/json")]
public class ErpTeacherRatesController(
    ITeacherRateRepository rateRepository,
    IClassTypeRepository classTypeRepository,
    IAuditService auditService) : ControllerBase
{
    private Guid AdminId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    private static object MapClassRate(ClassRate r) => new
    {
        id = r.Id,
        classTypeId = r.ClassTypeId,
        classTypeName = r.ClassType?.Name,
        value = r.Value,
        effectiveFrom = r.EffectiveFrom.ToString("yyyy-MM-dd"),
        createdAt = r.CreatedAt,
        createdById = r.CreatedById
    };

    private static object MapBonusRate(TeacherBonusRate r) => new
    {
        id = r.Id,
        valuePerStudent = r.ValuePerStudent,
        effectiveFrom = r.EffectiveFrom.ToString("yyyy-MM-dd"),
        createdAt = r.CreatedAt,
        createdById = r.CreatedById
    };

    [HttpGet("api/class-rates")]
    public async Task<IActionResult> ListClassRates(CancellationToken ct)
    {
        var rates = await rateRepository.ListClassRatesAsync(ct);
        return Ok(new { data = rates.Select(MapClassRate) });
    }

    [HttpPost("api/class-rates")]
    public async Task<IActionResult> CreateClassRate([FromBody] CreateClassRateRequest req, CancellationToken ct)
    {
        if (req.value < 0)
            throw DomainException.Validation("INVALID_VALUE", "O valor da aula não pode ser negativo.");

        if (req.effectiveFrom is null)
            throw DomainException.Validation("EFFECTIVE_FROM_REQUIRED", "A data de vigência é obrigatória.");

        // classTypeId nulo = valor padrão (vale para toda modalidade sem linha própria).
        if (req.classTypeId.HasValue)
        {
            _ = await classTypeRepository.GetByIdAsync(req.classTypeId.Value, ct)
                ?? throw DomainException.NotFound("Class type not found.");
        }

        var rate = ClassRate.Create(req.classTypeId, req.value, req.effectiveFrom.Value, AdminId);
        await rateRepository.AddClassRateAsync(rate, ct);
        await rateRepository.SaveAsync(ct);

        await auditService.LogAsync(
            "class_rate.created", "ClassRate", rate.Id, AdminId,
            $"ClassTypeId: {req.classTypeId?.ToString() ?? "default"}, Value: {req.value}, EffectiveFrom: {req.effectiveFrom:yyyy-MM-dd}",
            ct: ct);

        var created = (await rateRepository.ListClassRatesAsync(ct)).First(r => r.Id == rate.Id);
        return StatusCode(201, MapClassRate(created));
    }

    [HttpGet("api/teacher-bonus-rates")]
    public async Task<IActionResult> ListBonusRates(CancellationToken ct)
    {
        var rates = await rateRepository.ListBonusRatesAsync(ct);
        return Ok(new { data = rates.Select(MapBonusRate) });
    }

    [HttpPost("api/teacher-bonus-rates")]
    public async Task<IActionResult> CreateBonusRate([FromBody] CreateBonusRateRequest req, CancellationToken ct)
    {
        if (req.valuePerStudent < 0)
            throw DomainException.Validation("INVALID_VALUE", "O valor do bônus não pode ser negativo.");

        if (req.effectiveFrom is null)
            throw DomainException.Validation("EFFECTIVE_FROM_REQUIRED", "A data de vigência é obrigatória.");

        var rate = TeacherBonusRate.Create(req.valuePerStudent, req.effectiveFrom.Value, AdminId);
        await rateRepository.AddBonusRateAsync(rate, ct);
        await rateRepository.SaveAsync(ct);

        await auditService.LogAsync(
            "teacher_bonus_rate.created", "TeacherBonusRate", rate.Id, AdminId,
            $"ValuePerStudent: {req.valuePerStudent}, EffectiveFrom: {req.effectiveFrom:yyyy-MM-dd}",
            ct: ct);

        return StatusCode(201, MapBonusRate(rate));
    }
}

public record CreateClassRateRequest(Guid? classTypeId, decimal value, DateOnly? effectiveFrom);
public record CreateBonusRateRequest(decimal valuePerStudent, DateOnly? effectiveFrom);
