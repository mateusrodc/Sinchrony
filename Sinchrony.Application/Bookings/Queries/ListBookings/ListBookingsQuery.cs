using MediatR;
using Sinchrony.Application.Common;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Bookings.Queries.ListBookings;

public record ListBookingsQuery(
    Guid StudentId, string? Status, bool History,
    int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<BookingListItemDto>>;

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
    : IRequestHandler<ListBookingsQuery, PagedResult<BookingListItemDto>>
{
    public async Task<PagedResult<BookingListItemDto>> Handle(ListBookingsQuery request, CancellationToken ct)
    {
        var (items, total) = await bookingRepository.ListByStudentPagedAsync(
            request.StudentId, request.Status, request.History,
            request.Page, request.PageSize, ct);

        var data = items.Select(b => new BookingListItemDto(
            b.Id, b.ClassId,
            new ClassSummaryDto(
                b.Class!.Id, b.Class.Name,
                b.Class.ClassType?.Name ?? string.Empty,
                b.Class.Teacher?.Name ?? string.Empty,
                b.Class.Date.ToString("yyyy-MM-dd"),
                b.Class.StartTime, b.Class.EndTime, b.Class.Duration,
                new StudioSummaryDto(
                    b.Class.Studio!.Id,
                    b.Class.Studio.Name,
                    b.Class.Studio.Address)),
            b.BikeNumber, b.Status.ToString(),
            b.BookedAt, b.CheckedIn)).ToList();

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);
        return new PagedResult<BookingListItemDto>(data, request.Page, request.PageSize, total, totalPages);
    }
}