using MediatR;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Bookings.Queries.ListBookings;

public record ListBookingsQuery(Guid StudentId, string? Status, bool History) : IRequest<IEnumerable<BookingListItemDto>>;

public record BookingListItemDto(
    Guid Id,
    Guid ClassId,
    ClassSummaryDto Class,
    int? BikeNumber,
    string Status,
    DateTime BookedAt,
    bool CheckedIn);

public record ClassSummaryDto(
    Guid Id, string Name, string Type, string Instructor,
    string Date, string StartTime, string EndTime, int Duration,
    StudioSummaryDto Studio);

public record StudioSummaryDto(Guid Id, string Name, string Address);

public class ListBookingsQueryHandler(IBookingRepository bookingRepository)
    : IRequestHandler<ListBookingsQuery, IEnumerable<BookingListItemDto>>
{
    public async Task<IEnumerable<BookingListItemDto>> Handle(ListBookingsQuery request, CancellationToken ct)
    {
        var bookings = await bookingRepository.ListByStudentAsync(request.StudentId, request.Status, request.History, ct);

        return bookings.Select(b => new BookingListItemDto(
            b.Id,
            b.ClassId,
            new ClassSummaryDto(
                b.Class!.Id,
                b.Class.Name,
                b.Class.ClassType?.Name ?? string.Empty,
                b.Class.Teacher?.Name ?? string.Empty,
                b.Class.Date.ToString("yyyy-MM-dd"),
                b.Class.StartTime,
                b.Class.EndTime,
                b.Class.Duration,
                new StudioSummaryDto(
                    b.Class.Studio!.Id,
                    b.Class.Studio.Name,
                    b.Class.Studio.Address)),
            b.BikeNumber,
            b.Status.ToString(),
            b.BookedAt,
            b.CheckedIn));
    }
}