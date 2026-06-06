using MediatR;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Sessions.Commands.StartSession;

public record StartSessionCommand(Guid ClassId, Guid TeacherId) : IRequest<SessionDto>;

public record SessionDto(
    Guid Id, Guid ClassId, string Status,
    DateTime StartedAt, int Duration,
    int EnrolledCount, int AttendedCount,
    DateTime? EndedAt = null);

public class StartSessionCommandHandler(
    IClassRepository classRepository,
    ISessionRepository sessionRepository,
    IBookingRepository bookingRepository)
    : IRequestHandler<StartSessionCommand, SessionDto>
{
    public async Task<SessionDto> Handle(StartSessionCommand request, CancellationToken ct)
    {
        var @class = await classRepository.GetByIdAsync(request.ClassId, ct)
            ?? throw DomainException.NotFound("Class not found.");

        if (@class.TeacherId != request.TeacherId)
            throw DomainException.Forbidden("You are not the teacher of this class.");

        if (@class.Status != ClassStatus.scheduled)
            throw DomainException.Conflict("SESSION_CONFLICT", "Class is not in scheduled status.");

        @class.Start();
        var session = ClassSession.Create(request.ClassId, @class.Duration);
        await sessionRepository.AddAsync(session, ct);
        await classRepository.SaveAsync(ct);

        var enrolled = await bookingRepository.ListErpAsync(request.ClassId, null, null, ct);
        var activeCount = enrolled.Count(b => b.Status != BookingStatus.cancelled);
        var attendedCount = enrolled.Count(b => b.Status == BookingStatus.attended);

        return new SessionDto(session.Id, session.ClassId, session.Status.ToString(),
            session.StartedAt, session.Duration, activeCount, attendedCount);
    }
}