using FluentValidation;
using MediatR;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Domain.Scheduling;

namespace Sinchrony.Application.Classes.Commands.UpdateClass;

// EndTime é aceito por compatibilidade mas ignorado: a API calcula (StartTime + Duration).
public record UpdateClassCommand(
    Guid AdminId, Guid ClassId, string Name, Guid ClassTypeId, Guid TeacherId, Guid StudioId,
    string Date, string StartTime, string? EndTime, int Duration, int TotalSpots, string Status) : IRequest<Class>;

public class UpdateClassCommandValidator : AbstractValidator<UpdateClassCommand>
{
    public UpdateClassCommandValidator()
    {
        RuleFor(x => x.Name).ValidName();
        RuleFor(x => x.ClassTypeId).NotEmpty();
        RuleFor(x => x.TeacherId).NotEmpty();
        RuleFor(x => x.StudioId).NotEmpty();
        RuleFor(x => x.Date).ValidDate("date");
        RuleFor(x => x.StartTime).ValidStartTime();
        RuleFor(x => x.Duration).ValidDuration().EndsSameDay(x => x.StartTime);
        RuleFor(x => x.TotalSpots).ValidTotalSpots();
        RuleFor(x => x.Status).NotEmpty()
            .Must(s => Enum.TryParse<ClassStatus>(s, ignoreCase: true, out _)).WithMessage("status inválido.");
    }
}

public class UpdateClassCommandHandler(
    ClassPlanning planning,
    IClassRepository classRepository,
    IAuditService auditService) : IRequestHandler<UpdateClassCommand, Class>
{
    public async Task<Class> Handle(UpdateClassCommand r, CancellationToken ct)
    {
        var @class = await classRepository.GetByIdAsync(r.ClassId, ct)
            ?? throw DomainException.NotFound("Class not found.");

        if (!planning.CanManage(@class.Studio))
            throw DomainException.Forbidden("Você não tem permissão para gerenciar esta unidade.");
        if (r.StudioId != @class.StudioId)
            await planning.RequireManageableStudioAsync(r.StudioId, ct);
        await planning.RequireReferencesAsync(r.ClassTypeId, r.TeacherId, ct);

        var date = DateOnly.ParseExact(r.Date, "yyyy-MM-dd");
        var status = Enum.Parse<ClassStatus>(r.Status, ignoreCase: true);
        var previousStatus = @class.Status;
        var endTime = ClassSchedule.ComputeEndTime(r.StartTime, r.Duration);

        // O cancelamento por aqui segue a mesma regra do /deactivate: só sem reservas ativas.
        if (status == ClassStatus.cancelled && previousStatus != ClassStatus.cancelled)
            @class.EnsureNoActiveBookings();

        var scheduleChanged = date != @class.Date || r.StartTime != @class.StartTime
            || r.Duration != @class.Duration || r.StudioId != @class.StudioId || r.TeacherId != @class.TeacherId;
        // Só o que afeta a agenda do professor pode gerar conflito bloqueante (trocar só a sala não).
        var teacherSlotChanged = date != @class.Date || r.StartTime != @class.StartTime
            || r.Duration != @class.Duration || r.TeacherId != @class.TeacherId;
        var contentChanged = scheduleChanged || r.Name != @class.Name
            || r.ClassTypeId != @class.ClassTypeId || r.TotalSpots != @class.TotalSpots;

        // Só barra conflito novo de professor (sala não bloqueia): aula que já estava em conflito no
        // banco segue editável (nome, vagas…) enquanto professor/horário não mudam. Reativar via PUT
        // também ocupa o horário de novo.
        var occupiesSlot = status != ClassStatus.cancelled;
        var reactivating = previousStatus == ClassStatus.cancelled && occupiesSlot;
        var mustCheckConflicts = occupiesSlot && (teacherSlotChanged || reactivating);

        async Task<bool> ApplyAsync()
        {
            if (mustCheckConflicts)
            {
                var conflicts = await planning.FindBlockingConflictsAsync(
                    date, r.StartTime, endTime, r.StudioId, r.TeacherId, @class.Id, ct);
                if (conflicts.Count > 0) throw ClassPlanning.ConflictError(conflicts);
            }

            @class.Update(r.Name, r.ClassTypeId, r.TeacherId, r.StudioId,
                date, r.StartTime, endTime, r.Duration, r.TotalSpots, status);

            // Edição individual de ocorrência de série: sai da edição em grupo (status sozinho não conta,
            // igual ao /deactivate e /activate).
            if (contentChanged) @class.MarkAsException();

            await classRepository.SaveAsync(ct);
            return true;
        }

        // A checagem + gravação rodam sob a trava do professor para não passarem duas edições juntas.
        if (mustCheckConflicts) await planning.WithTeacherLockAsync(r.TeacherId, ApplyAsync, ct);
        else await ApplyAsync();

        // Não existe endpoint dedicado de cancelamento de aula — é feito via este PUT com
        // status "cancelled". Registrado separadamente por ser a mudança mais impactante
        // (mexe em quem já reservou), o resto do update fica num log só mais genérico.
        await auditService.LogAsync(
            previousStatus != status ? "class.status_changed" : "class.updated",
            "Class", @class.Id, r.AdminId,
            previousStatus != status ? $"From: {previousStatus} To: {status}" : $"Name: {@class.Name}",
            ct: ct);

        return (await classRepository.GetByIdAsync(@class.Id, ct))!;
    }
}
