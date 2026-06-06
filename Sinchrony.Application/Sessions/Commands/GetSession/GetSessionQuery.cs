using MediatR;
using Sinchrony.Application.Sessions.Commands.StartSession;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Sessions.Queries.GetSession;

public record GetSessionQuery(Guid ClassId) : IRequest<SessionDto?>;

public class GetSessionQueryHandler(
    ISessionRepository sessionRepository,
    IBookingRepository bookingRepository)
    : IRequestHandler<GetSessionQuery, SessionDto?>
{
    public async Task<SessionDto?> Handle(GetSessionQuery request, CancellationToken ct)
    {
        var session = await sessionRepository.GetByClassIdAsync(request.ClassId, ct);
        if (session is null) return null;

        var enrolled = await bookingRepository.ListErpAsync(request.ClassId, null, null, ct);
        var activeCount = enrolled.Count(b => b.Status != BookingStatus.cancelled);
        var attendedCount = enrolled.Count(b => b.Status == BookingStatus.attended);

        return new SessionDto(session.Id, session.ClassId, session.Status.ToString(),
            session.StartedAt, session.Duration, activeCount, attendedCount, session.EndedAt);
    }
}