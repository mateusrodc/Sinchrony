namespace Sinchrony.Domain.Entities;

// Prova do aceite dos Termos de Uso: uma linha por aceite, só inclusão — nunca é alterada nem
// apagada. User.TermsAcceptedAt/TermsVersion continuam como espelho do aceite mais recente.
public class TermsAcceptance
{
    public const string ChannelAppRegister = "app_register";
    public const string ChannelLegacy = "legacy";

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string TermsVersion { get; private set; } = string.Empty;

    // Hora do servidor (UTC) — é a que vale como prova.
    public DateTime AcceptedAt { get; private set; }

    // Horário informado pelo aparelho; informativo, pode estar errado.
    public DateTime? ClientAcceptedAt { get; private set; }

    public string Channel { get; private set; } = ChannelAppRegister;
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? AppVersion { get; private set; }

    public User? User { get; private set; }

    protected TermsAcceptance() { }

    public static TermsAcceptance Create(
        Guid userId, string termsVersion, string channel,
        DateTime? clientAcceptedAt, string? ipAddress, string? userAgent, string? appVersion)
        => new()
        {
            UserId = userId,
            TermsVersion = termsVersion,
            AcceptedAt = DateTime.UtcNow,
            ClientAcceptedAt = clientAcceptedAt,
            Channel = channel,
            IpAddress = Truncate(ipAddress, 45),
            UserAgent = Truncate(userAgent, 500),
            AppVersion = Truncate(appVersion, 30)
        };

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }
}
