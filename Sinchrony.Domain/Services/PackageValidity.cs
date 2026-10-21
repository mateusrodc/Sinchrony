namespace Sinchrony.Domain.Services;

// Regra (21/10/2026 — DEMANDA_VALIDADE_FIM_DO_DIA_E_FILA_AO_ZERAR_CREDITOS_BACKEND.md): a
// validade de um pacote vai até o último minuto do último dia, em horário de Brasília — não
// mais até a mesma hora da compra. O último dia continua sendo "data de início (BRT) + dias":
// comprado em 21/09 às 16:25 com 30 dias, vale até 21/10 às 23:59:59 BRT (22/10 02:59:59 UTC).
// Único ponto que calcula EndDate — Create, Activate, ExtendValidity, RenewCycle e ChargeRenewed
// passam todos por aqui, pra não divergir.
public static class PackageValidity
{
    private static readonly TimeSpan EndOfDay = new(23, 59, 59);

    public static DateTime End(DateTime startUtc, int days)
    {
        var lastDay = ToBrasilia(startUtc).Date.AddDays(days);
        return ToUtc(lastDay + EndOfDay);
    }

    // Mesmo cálculo do BrasiliaTime (Application) — o Domain não referencia a Application.
    private static DateTime ToBrasilia(DateTime utc)
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

    private static DateTime ToUtc(DateTime local)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), tz);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateTime.SpecifyKind(local.AddHours(3), DateTimeKind.Utc);
        }
    }
}
