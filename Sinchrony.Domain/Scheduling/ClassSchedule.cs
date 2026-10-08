using System.Globalization;

namespace Sinchrony.Domain.Scheduling;

// Regras puras de horário de aula. Horários são "HH:mm" locais (sem fuso), como em classes.StartTime.
public static class ClassSchedule
{
    public const int MinDuration = 15;
    public const int MaxDuration = 240;
    public const int MinSpots = 1;
    public const int MaxSpots = 200;

    public static bool TryParseTime(string? value, out int minutes)
    {
        minutes = 0;
        if (value is null || value.Length != 5 || value[2] != ':') return false;
        if (!int.TryParse(value.AsSpan(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var h)
            || !int.TryParse(value.AsSpan(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var m))
            return false;
        if (h > 23 || m > 59) return false;
        minutes = h * 60 + m;
        return true;
    }

    // Aula não atravessa a meia-noite: o fim precisa cair no mesmo dia.
    public static bool EndsSameDay(string startTime, int duration)
        => TryParseTime(startTime, out var start) && start + duration <= 23 * 60 + 59;

    public static string ComputeEndTime(string startTime, int duration)
    {
        TryParseTime(startTime, out var start);
        var end = start + duration;
        return $"{end / 60:00}:{end % 60:00}";
    }

    // Sobreposição de intervalos [início, fim): aulas encostadas (fim == início) não conflitam.
    public static bool Overlaps(string startA, string endA, string startB, string endB)
    {
        if (!TryParseTime(startA, out var a1) || !TryParseTime(endA, out var a2)
            || !TryParseTime(startB, out var b1) || !TryParseTime(endB, out var b2))
            return false;
        return a1 < b2 && b1 < a2;
    }

    // Datas da regra semanal entre início e fim, ambos inclusivos. DayOfWeek: 0 = domingo … 6 = sábado.
    public static IReadOnlyList<DateOnly> GenerateDates(IEnumerable<int> daysOfWeek, DateOnly start, DateOnly end)
    {
        var days = daysOfWeek.ToHashSet();
        var result = new List<DateOnly>();
        for (var d = start; d <= end; d = d.AddDays(1))
            if (days.Contains((int)d.DayOfWeek)) result.Add(d);
        return result;
    }
}
