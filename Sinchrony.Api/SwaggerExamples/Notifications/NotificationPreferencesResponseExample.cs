using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Notifications;

public class NotificationPreferencesResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        pushEnabled = true,
        emailEnabled = true,
        preferences = new[]
        {
            new { id = "class_reminder",     title = "Lembrete de aula",      enabled = true },
            new { id = "class_cancellation", title = "Cancelamento de aula",  enabled = true },
            new { id = "promotions",         title = "Promoções e ofertas",   enabled = false },
            new { id = "new_modalities",     title = "Novas modalidades",     enabled = true },
            new { id = "payment_result",     title = "Resultado de pagamento",enabled = true },
            new { id = "referral",           title = "Bring a Friend",        enabled = false }
        }
    };
}