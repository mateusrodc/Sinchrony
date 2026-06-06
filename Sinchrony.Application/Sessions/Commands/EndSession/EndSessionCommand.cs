using MediatR;
using Sinchrony.Application.Sessions.Commands.StartSession;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Sessions.Commands.EndSession;

public record EndSessionCommand(Guid ClassId, Guid TeacherId) : IRequest<SessionDto>;

public class EndSessionCommandHandler(
    IClassRepository classRepository,
    ISessionRepository sessionRepository,
    IBookingRepository bookingRepository)
    : IRequestHandler<EndSessionCommand, Application.Sessions.Commands.StartSession.SessionDto>
{
    public async Task<Application.Sessions.Commands.StartSession.SessionDto> Handle(EndSessionCommand request, CancellationToken ct)
    {
        var @class = await classRepository.GetByIdAsync(request.ClassId, ct)
            ?? throw DomainException.NotFound("Class not found.");

        if (@class.TeacherId != request.TeacherId)
            throw DomainException.Forbidden("You are not the teacher of this class.");

        if (@class.Status != ClassStatus.in_progress)
            throw DomainException.Conflict("SESSION_NOT_STARTED", "Class is not in progress.");

        var session = await sessionRepository.GetByClassIdAsync(request.ClassId, ct)
            ?? throw DomainException.NotFound("Session not found.");

        @class.Complete();
        session.End();
        await classRepository.SaveAsync(ct);
        await sessionRepository.SaveAsync(ct);

        var enrolled = await bookingRepository.ListErpAsync(request.ClassId, null, null, ct);
        var activeCount = enrolled.Count(b => b.Status != BookingStatus.cancelled);
        var attendedCount = enrolled.Count(b => b.Status == BookingStatus.attended);

        return new Application.Sessions.Commands.StartSession.SessionDto(
            session.Id, session.ClassId, session.Status.ToString(),
            session.StartedAt, session.Duration, activeCount, attendedCount, session.EndedAt);
    }
}