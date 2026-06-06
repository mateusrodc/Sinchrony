using MediatR;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Classes.Queries.GetClassBikes;

public record GetClassBikesQuery(Guid ClassId) : IRequest<IEnumerable<ClassBikeDto>>;

public record ClassBikeDto(int Number, string Status);

public class GetClassBikesQueryHandler(
    IClassRepository classRepository,
    IBookingRepository bookingRepository,
    IBikeRepository bikeRepository)
    : IRequestHandler<GetClassBikesQuery, IEnumerable<ClassBikeDto>>
{
    public async Task<IEnumerable<ClassBikeDto>> Handle(GetClassBikesQuery request, CancellationToken ct)
    {
        var @class = await classRepository.GetByIdAsync(request.ClassId, ct)
            ?? throw DomainException.NotFound("Class not found.");

        var bikes = await bikeRepository.ListByStudioAsync(@class.StudioId, ct);
        var bookings = await bookingRepository.ListErpAsync(request.ClassId, null, null, ct);

        var occupiedBikes = bookings
            .Where(b => b.Status != BookingStatus.cancelled && b.BikeNumber.HasValue)
            .Select(b => b.BikeNumber!.Value)
            .ToHashSet();

        return bikes.Select(b => new ClassBikeDto(
            b.Number,
            b.Status == BikeStatus.available && !occupiedBikes.Contains(b.Number)
                ? "available"
                : occupiedBikes.Contains(b.Number) ? "occupied" : b.Status.ToString()));
    }
}