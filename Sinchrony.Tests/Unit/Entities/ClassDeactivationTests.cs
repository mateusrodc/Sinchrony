using FluentAssertions;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Xunit;

namespace Sinchrony.Tests.Unit.Entities;

// DEMANDA_EXCLUIR_CADASTROS_ERP_BACKEND.md item 4 — desativar aula = cancelled, só sem reservas ativas.
public class ClassDeactivationTests
{
    private static readonly DateOnly Today = new(2026, 9, 18);

    private static Class CreateClass(DateOnly? date = null) =>
        Class.Create("Spinning", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            date ?? Today.AddDays(1), "07:00", "08:00", 60, 20);

    private static Booking AddBooking(Class c, Action<Booking>? configure = null)
    {
        var b = Booking.Create(c.Id, Guid.NewGuid(), null);
        configure?.Invoke(b);
        c.Bookings.Add(b);
        return b;
    }

    [Fact]
    public void Deactivate_WithoutBookings_CancelsClass()
    {
        var c = CreateClass();

        c.Deactivate();

        c.Status.Should().Be(ClassStatus.cancelled);
    }

    [Fact]
    public void Deactivate_OnlyCancelledBookings_IsAllowed()
    {
        var c = CreateClass();
        AddBooking(c, b => b.Cancel());

        c.Deactivate();

        c.Status.Should().Be(ClassStatus.cancelled);
    }

    [Theory]
    [InlineData("confirmed")]
    [InlineData("attended")]
    [InlineData("no_show")]
    public void Deactivate_WithActiveBooking_ThrowsConflictAndKeepsStatus(string bookingState)
    {
        var c = CreateClass();
        AddBooking(c, b =>
        {
            if (bookingState == "attended") b.MarkAttended();
            if (bookingState == "no_show") b.MarkNoShow();
        });

        var act = () => c.Deactivate();

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "CLASS_HAS_BOOKINGS" && e.HttpStatus == 409);
        c.Status.Should().Be(ClassStatus.scheduled);
    }

    [Theory]
    [InlineData(ClassStatus.in_progress)]
    [InlineData(ClassStatus.completed)]
    [InlineData(ClassStatus.cancelled)]
    public void Deactivate_WhenNotScheduled_ThrowsConflict(ClassStatus status)
    {
        var c = CreateClass();
        c.Update(c.Name, c.ClassTypeId, c.TeacherId, c.StudioId, c.Date, c.StartTime, c.EndTime,
            c.Duration, c.TotalSpots, status);

        var act = () => c.Deactivate();

        act.Should().Throw<DomainException>().Where(e => e.Code == "CLASS_NOT_SCHEDULED");
    }

    [Fact]
    public void Reactivate_CancelledFutureClass_BecomesScheduled()
    {
        var c = CreateClass();
        c.Deactivate();

        c.Reactivate(Today);

        c.Status.Should().Be(ClassStatus.scheduled);
    }

    [Fact]
    public void Reactivate_ClassToday_IsAllowed()
    {
        var c = CreateClass(Today);
        c.Deactivate();

        c.Reactivate(Today);

        c.Status.Should().Be(ClassStatus.scheduled);
    }

    [Fact]
    public void Reactivate_PastClass_ThrowsConflict()
    {
        var c = CreateClass(Today.AddDays(-1));
        c.Deactivate();

        var act = () => c.Reactivate(Today);

        act.Should().Throw<DomainException>().Where(e => e.Code == "CLASS_IN_PAST");
        c.Status.Should().Be(ClassStatus.cancelled);
    }

    [Fact]
    public void Reactivate_WhenNotCancelled_ThrowsConflict()
    {
        var c = CreateClass();

        var act = () => c.Reactivate(Today);

        act.Should().Throw<DomainException>().Where(e => e.Code == "CLASS_NOT_CANCELLED");
    }
}
