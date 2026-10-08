using FluentValidation;
using MediatR;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Domain.Scheduling;

namespace Sinchrony.Application.Classes.Commands.CreateClass;

// EndTime é aceito por compatibilidade mas ignorado: a API calcula (StartTime + Duration).
public record CreateClassCommand(
    Guid AdminId, string Name, Guid ClassTypeId, Guid TeacherId, Guid StudioId,
    string Date, string StartTime, string? EndTime, int Duration, int TotalSpots) : IRequest<Class>;

public class CreateClassCommandValidator : AbstractValidator<CreateClassCommand>
{
    public CreateClassCommandValidator()
    {
        RuleFor(x => x.Name).ValidName();
        RuleFor(x => x.ClassTypeId).NotEmpty();
        RuleFor(x => x.TeacherId).NotEmpty();
        RuleFor(x => x.StudioId).NotEmpty();
        RuleFor(x => x.Date).ValidDate("date");
        RuleFor(x => x.StartTime).ValidStartTime();
        RuleFor(x => x.Duration).ValidDuration().EndsSameDay(x => x.StartTime);
        RuleFor(x => x.TotalSpots).ValidTotalSpots();
    }
}

public class CreateClassCommandHandler(
    ClassPlanning planning,
    IClassRepository classRepository,
    IAuditService auditService) : IRequestHandler<CreateClassCommand, Class>
{
    public async Task<Class> Handle(CreateClassCommand r, CancellationToken ct)
    {
        await planning.RequireManageableStudioAsync(r.StudioId, ct);
        await planning.RequireReferencesAsync(r.ClassTypeId, r.TeacherId, ct);

        var date = DateOnly.ParseExact(r.Date, "yyyy-MM-dd");
        if (date < planning.Today)
            throw DomainException.Conflict("CLASS_IN_PAST", "Não é possível criar uma aula com data passada.");

        var endTime = ClassSchedule.ComputeEndTime(r.StartTime, r.Duration);

        var conflicts = await planning.FindConflictsAsync(
            date, r.StartTime, endTime, r.StudioId, r.TeacherId, null, ct);
        if (conflicts.Count > 0) throw ClassPlanning.ConflictError(conflicts);

        var @class = Class.Create(r.Name, r.ClassTypeId, r.TeacherId, r.StudioId,
            date, r.StartTime, endTime, r.Duration, r.TotalSpots);

        await classRepository.AddAsync(@class, ct);
        await classRepository.SaveAsync(ct);

        await auditService.LogAsync("class.created", "Class", @class.Id, r.AdminId, $"Name: {@class.Name}", ct: ct);

        return (await classRepository.GetByIdAsync(@class.Id, ct))!;
    }
}
