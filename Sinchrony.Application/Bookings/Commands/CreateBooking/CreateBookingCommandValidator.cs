using FluentValidation;

namespace Sinchrony.Application.Bookings.Commands.CreateBooking;

public class CreateBookingCommandValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingCommandValidator()
    {
        RuleFor(x => x.ClassId).NotEmpty();
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.BikeNumber).GreaterThan(0).When(x => x.BikeNumber.HasValue);
    }
}