using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Services;

public static class PackageRuleResolver
{
    public static int GetBookingWindowDays(StudentPackage? sp, Settings settings)
        => sp?.Package?.BookingWindowDays
        ?? sp?.Package?.PackageType?.DefaultBookingWindowDays
        ?? settings.BookingWindowDays;

    public static int GetCancellationDeadlineHours(StudentPackage? sp, Settings settings)
        => sp?.Package?.CancellationDeadlineHours
        ?? sp?.Package?.PackageType?.DefaultCancellationDeadlineHours
        ?? settings.CancellationDeadlineHours;

    public static int? GetMaxFutureBookings(StudentPackage? sp)
        => sp?.Package?.MaxFutureBookings
        ?? sp?.Package?.PackageType?.DefaultMaxFutureBookings;

    public static int? GetMaxBookingsPerDay(StudentPackage? sp)
        => sp?.Package?.MaxBookingsPerDay
        ?? sp?.Package?.PackageType?.DefaultMaxBookingsPerDay;

    public static int? GetMaxBookingsPerWeek(StudentPackage? sp)
        => sp?.Package?.MaxBookingsPerWeek
        ?? sp?.Package?.PackageType?.DefaultMaxBookingsPerWeek;

    public static int? GetMaxBookingsPerMonth(StudentPackage? sp)
        => sp?.Package?.MaxBookingsPerMonth
        ?? sp?.Package?.PackageType?.DefaultMaxBookingsPerMonth;

    public static int? GetEarlyAccessHours(StudentPackage? sp)
        => sp?.Package?.EarlyAccessHours
        ?? sp?.Package?.PackageType?.DefaultEarlyAccessHours;

    public static bool GetAllowWaitlist(StudentPackage? sp, Settings settings)
        => sp?.Package?.AllowWaitlist
        ?? sp?.Package?.PackageType?.DefaultAllowWaitlist
        ?? settings.AllowWaitlist;

    public static bool GetReschedulingAllowed(StudentPackage? sp)
        => sp?.Package?.ReschedulingAllowed
        ?? sp?.Package?.PackageType?.DefaultReschedulingAllowed
        ?? false;

    public static int GetReschedulingDeadlineHours(StudentPackage? sp)
        => sp?.Package?.ReschedulingDeadlineHours
        ?? sp?.Package?.PackageType?.DefaultReschedulingDeadlineHours
        ?? 24;

    public static bool GetNoShowCreditPenalty(StudentPackage? sp)
        => sp?.Package?.NoShowCreditPenalty
        ?? sp?.Package?.PackageType?.DefaultNoShowCreditPenalty
        ?? true;

    public static int? GetMaxNoShowsBeforeBlock(StudentPackage? sp)
        => sp?.Package?.MaxNoShowsBeforeBlock
        ?? sp?.Package?.PackageType?.DefaultMaxNoShowsBeforeBlock;

    public static int GetNoShowBlockWindowDays(StudentPackage? sp)
        => sp?.Package?.NoShowBlockWindowDays ?? 30;
}