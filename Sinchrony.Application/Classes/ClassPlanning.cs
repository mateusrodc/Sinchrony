using Sinchrony.Application.Common;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Classes;

// Checagens comuns a cadastro/edição de aula avulsa e de série: existência das referências,
// unidade do admin e "hoje" em horário de Brasília (Class.Date é data de calendário local).
public class ClassPlanning(
    IStudioRepository studioRepository,
    IClassTypeRepository classTypeRepository,
    IUserRepository userRepository,
    IClassRepository classRepository,
    IUnitContext unitContext,
    TimeProvider clock)
{
    public DateOnly Today => BrasiliaTime.ToDate(clock.GetUtcNow().UtcDateTime);

    // Falha fechado: admin que não é global precisa ter unidade e a sala precisa ser dela.
    public bool CanManage(Studio? studio)
        => unitContext.IsGlobalAdmin
           || (unitContext.UnitId.HasValue && studio?.UnitId == unitContext.UnitId);

    public async Task<Studio> RequireManageableStudioAsync(Guid studioId, CancellationToken ct)
    {
        var studio = await studioRepository.GetByIdAsync(studioId, ct)
            ?? throw DomainException.NotFound("Studio not found.");
        if (!CanManage(studio))
            throw DomainException.Forbidden("Você não tem permissão para gerenciar esta unidade.");
        return studio;
    }

    public async Task RequireReferencesAsync(Guid classTypeId, Guid teacherId, CancellationToken ct)
    {
        _ = await classTypeRepository.GetByIdAsync(classTypeId, ct)
            ?? throw DomainException.NotFound("Class type not found.");
        _ = await userRepository.GetByIdAsync(teacherId, ct)
            ?? throw DomainException.NotFound("Teacher not found.");
    }

    public async Task<List<ClassConflictDto>> FindConflictsAsync(
        DateOnly date, string startTime, string endTime, Guid studioId, Guid teacherId,
        Guid? ignoreClassId, CancellationToken ct)
    {
        var candidates = await classRepository.ListSchedulingCandidatesAsync(date, date, studioId, teacherId, ct);
        return ClassConflictChecker.Find(date, startTime, endTime, studioId, teacherId, candidates, ignoreClassId);
    }

    public static DomainException ConflictError(IReadOnlyList<ClassConflictDto> conflicts)
        => DomainException.Conflict("CLASS_CONFLICT",
            "Já existe uma aula nesse horário na mesma sala ou com o mesmo professor.",
            new Dictionary<string, object?> { ["conflicts"] = conflicts });
}
