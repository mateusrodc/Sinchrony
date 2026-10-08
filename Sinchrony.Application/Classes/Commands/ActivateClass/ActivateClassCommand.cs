using MediatR;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Classes.Commands.ActivateClass;

public record ActivateClassCommand(Guid AdminId, Guid ClassId) : IRequest<Class>;

// Reativar ocupa o horário de novo: o professor não pode ficar em duas aulas ao mesmo tempo
// (mesma regra do PUT com status). Sala não bloqueia.
public class ActivateClassCommandHandler(
    ClassPlanning planning,
    IClassRepository classRepository,
    IAuditService auditService) : IRequestHandler<ActivateClassCommand, Class>
{
    public async Task<Class> Handle(ActivateClassCommand r, CancellationToken ct)
    {
        var @class = await classRepository.GetByIdAsync(r.ClassId, ct)
            ?? throw DomainException.NotFound("Class not found.");

        if (!planning.CanManage(@class.Studio))
            throw DomainException.Forbidden("Você não tem permissão para gerenciar esta unidade.");

        await planning.WithTeacherLockAsync(@class.TeacherId, async () =>
        {
            // Datas de aula são horário local (UTC-3); Today já vem em horário de Brasília.
            @class.Reactivate(planning.Today);

            var conflicts = await planning.FindBlockingConflictsAsync(
                @class.Date, @class.StartTime, @class.EndTime, @class.StudioId, @class.TeacherId, @class.Id, ct);
            if (conflicts.Count > 0) throw ClassPlanning.ConflictError(conflicts);

            await classRepository.SaveAsync(ct);
            return true;
        }, ct);

        await auditService.LogAsync("class.status_changed", "Class", @class.Id, r.AdminId,
            $"From: {ClassStatus.cancelled} To: {ClassStatus.scheduled}", ct: ct);

        return @class;
    }
}
