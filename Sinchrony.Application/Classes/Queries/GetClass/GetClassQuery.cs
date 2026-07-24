using MediatR;
using Sinchrony.Application.Classes.Queries.ListClasses;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Classes.Queries.GetClass;

public record GetClassQuery(Guid Id) : IRequest<ClassDetailDto>;

public record ClassDetailDto(
    Guid Id, string Name, string Type, Guid ClassTypeId,
    string Instructor, string? InstructorAvatar, Guid TeacherId,
    string Date, string StartTime, string EndTime, int Duration,
    int TotalSpots, int AvailableSpots, int EnrolledCount,
    string Status, StudioDto Studio,
    IEnumerable<ClassBikeItemDto>? Bikes = null);

public record ClassBikeItemDto(int Number, string Status);

public class GetClassQueryHandler(IClassRepository classRepository, IBikeRepository bikeRepository)
    : IRequestHandler<GetClassQuery, ClassDetailDto>
{
    public async Task<ClassDetailDto> Handle(GetClassQuery request, CancellationToken ct)
    {
        var @class = await classRepository.GetByIdAsync(request.Id, ct)
            ?? throw DomainException.NotFound("Class not found.");

        var enrolled = @class.Bookings.Count(b => b.Status != BookingStatus.cancelled);
        var isBike = @class.ClassType?.Name.ToLower().Contains("bike") == true;

        IEnumerable<ClassBikeItemDto>? bikes = null;
        if (isBike)
        {
            var occupiedBikes = @class.Bookings
                .Where(b => b.Status != BookingStatus.cancelled && b.BikeNumber.HasValue)
                .Select(b => b.BikeNumber!.Value)
                .ToHashSet();

            var studioB = await bikeRepository.ListByStudioAsync(@class.StudioId, ct);
            bikes = studioB.Select(b => new ClassBikeItemDto(
                b.Number,
                occupiedBikes.Contains(b.Number) ? "occupied" : b.Status.ToString()));
        }

        return new ClassDetailDto(
            @class.Id, @class.Name,
            @class.ClassType?.Name ?? string.Empty,
            @class.ClassTypeId,
            @class.Teacher?.Name ?? string.Empty,
            @class.Teacher?.Avatar,
            @class.TeacherId,
            @class.Date.ToString("yyyy-MM-dd"),
            @class.StartTime, @class.EndTime, @class.Duration,
            @class.TotalSpots,
            @class.TotalSpots - enrolled,
            enrolled,
            @class.Status.ToString(),
            new StudioDto(
                @class.Studio!.Id, @class.Studio.Name, @class.Studio.Address,
                @class.Studio.Capacity, @class.Studio.OpeningTime, @class.Studio.ClosingTime, @class.Studio.UnitId),
            bikes);
    }
}