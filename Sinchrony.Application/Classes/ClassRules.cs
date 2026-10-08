using FluentValidation;
using Sinchrony.Domain.Scheduling;

namespace Sinchrony.Application.Classes;

// Regras de formato de aula compartilhadas entre aula avulsa e série (antes só existiam nos formulários).
public static class ClassRules
{
    public static IRuleBuilderOptions<T, string> ValidName<T>(this IRuleBuilder<T, string> rule)
        => rule.NotEmpty().MaximumLength(100);

    public static IRuleBuilderOptions<T, string> ValidStartTime<T>(this IRuleBuilder<T, string> rule)
        => rule.NotEmpty()
            .Must(v => ClassSchedule.TryParseTime(v, out _)).WithMessage("startTime deve estar no formato HH:mm.");

    public static IRuleBuilderOptions<T, int> ValidDuration<T>(this IRuleBuilder<T, int> rule)
        => rule.InclusiveBetween(ClassSchedule.MinDuration, ClassSchedule.MaxDuration);

    public static IRuleBuilderOptions<T, int> ValidTotalSpots<T>(this IRuleBuilder<T, int> rule)
        => rule.InclusiveBetween(ClassSchedule.MinSpots, ClassSchedule.MaxSpots);

    public static IRuleBuilderOptions<T, int> EndsSameDay<T>(
        this IRuleBuilder<T, int> rule, Func<T, string> startTime)
        => rule.Must((x, duration) =>
                !ClassSchedule.TryParseTime(startTime(x), out _)
                || duration is < ClassSchedule.MinDuration or > ClassSchedule.MaxDuration
                || ClassSchedule.EndsSameDay(startTime(x), duration))
            .WithMessage("A aula deve terminar no mesmo dia.");

    public static IRuleBuilderOptions<T, string> ValidDate<T>(this IRuleBuilder<T, string> rule, string field)
        => rule.NotEmpty()
            .Must(v => DateOnly.TryParseExact(v, "yyyy-MM-dd", out _)).WithMessage($"{field} deve estar no formato yyyy-MM-dd.");
}
