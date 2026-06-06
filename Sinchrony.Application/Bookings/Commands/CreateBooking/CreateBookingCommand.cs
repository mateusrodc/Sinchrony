using MediatR;

namespace Sinchrony.Application.Bookings.Commands.CreateBooking;

public record CreateBookingCommand(Guid StudentId, Guid ClassId, int? BikeNumber) : IRequest<BookingDto>;

public record BookingDto(
    Guid Id, Guid ClassId, int? BikeNumber,
    string Status, DateTime BookedAt, bool CheckedIn);