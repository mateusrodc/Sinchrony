using MediatR;
using Sinchrony.Application.Classes.Queries.ListClasses;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Profile.Queries.ProfileProgress;

public record ProfileProgressQuery(Guid UserId) : IRequest<ProfileProgressDto>;

public record ProfileProgressDto(
    int ClassesAttended, int ClassesGoal,
    int StreakWeeks, int ActiveBookings,
    ClassDto? NextClass, int Credits);

public class ProfileProgressQueryHandler(
    IUserRepository userRepository,
    IBookingRepository bookingRepository,
    IClassRepository classRepository)
    : IRequestHandler<ProfileProgressQuery, ProfileProgressDto>
{
    public async Task<ProfileProgressDto> Handle(ProfileProgressQuery request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw DomainException.NotFound("User not found.");

        var bookings = (await bookingRepository.ListByStudentAsync(request.UserId, null, true, ct)).ToList();
        var attended = bookings.Count(b => b.Status == BookingStatus.attended);
        var active = bookings.Count(b => b.Status == BookingStatus.confirmed);

        var upcoming = bookings
            .Where(b => b.Status == BookingStatus.confirmed && b.Class?.Date >= DateOnly.FromDateTime(DateTime.UtcNow))
            .OrderBy(b => b.Class!.Date).ThenBy(b => b.Class!.StartTime)
            .FirstOrDefault();

        ClassDto? nextClass = upcoming?.Class is not null
            ? ListClassesQueryHandler.MapToDto(upcoming.Class)
            : null;

        return new ProfileProgressDto(attended, 50, 0, active, nextClass, user.Credits);
    }
}