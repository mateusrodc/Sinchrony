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
    IUnitOfWork unitOfWork,
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

    public async Task<List<ClassConflictDto>> FindBlockingConflictsAsync(
        DateOnly date, string startTime, string endTime, Guid studioId, Guid teacherId,
        Guid? ignoreClassId, CancellationToken ct)
    {
        var candidates = await classRepository.ListSchedulingCandidatesAsync(date, date, studioId, teacherId, ct);
        return ClassConflictChecker.Blocking(
            ClassConflictChecker.Find(date, startTime, endTime, studioId, teacherId, candidates, ignoreClassId));
    }

    // Chave da trava de agenda do professor: serializa criações/edições concorrentes do mesmo professor,
    // que de outro modo passariam juntas pela checagem de conflito.
    public static string TeacherLockKey(Guid teacherId) => $"class-schedule-teacher:{teacherId}";

    // Roda `work` numa transação com a trava do professor. Não usar dentro de transação já aberta
    // (a série abre a sua e pega a trava direto).
    public async Task<T> WithTeacherLockAsync<T>(Guid teacherId, Func<Task<T>> work, CancellationToken ct)
    {
        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await unitOfWork.AcquireAdvisoryLockAsync(TeacherLockKey(teacherId), ct);
            var result = await work();
            await unitOfWork.CommitAsync(ct);
            return result;
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }
    }

    public static DomainException ConflictError(IReadOnlyList<ClassConflictDto> conflicts)
        => DomainException.Conflict("CLASS_CONFLICT",
            "O professor já tem uma aula nesse horário.",
            new Dictionary<string, object?> { ["conflicts"] = conflicts });
}
