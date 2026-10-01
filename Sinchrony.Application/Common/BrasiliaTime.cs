namespace Sinchrony.Application.Common;

// Conversão entre UTC e horário de Brasília, usada em DTOs (nextDueDate), no corpo de e-mails e
// em comparações contra horários locais gravados no banco (ex: Class.Date/StartTime).
// Sem horário de verão desde 2019 (fallback fixo se o TZ do SO não tiver a entrada IANA).
public static class BrasiliaTime
{
    public static DateTime Convert(DateTime utc)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);
        }
        catch (TimeZoneNotFoundException)
        {
            return utc.AddHours(-3);
        }
    }

    public static DateTime ToUtc(DateTime local)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), tz);
        }
        catch (TimeZoneNotFoundException)
        {
            return local.AddHours(3);
        }
    }

    public static DateOnly ToDate(DateTime utc) => DateOnly.FromDateTime(Convert(utc));
}
