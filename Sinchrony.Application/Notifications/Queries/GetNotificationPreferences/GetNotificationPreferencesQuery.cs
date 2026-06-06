using MediatR;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Notifications.Queries.GetNotificationPreferences;

public record GetNotificationPreferencesQuery(Guid UserId) : IRequest<NotificationPreferencesDto>;

public record NotificationPreferencesDto(
    bool PushEnabled, bool EmailEnabled,
    List<PreferenceItemDto> Preferences);

public record PreferenceItemDto(string Id, string Title, bool Enabled);

public class GetNotificationPreferencesQueryHandler(INotificationPreferenceRepository repository)
    : IRequestHandler<GetNotificationPreferencesQuery, NotificationPreferencesDto>
{
    public async Task<NotificationPreferencesDto> Handle(GetNotificationPreferencesQuery request, CancellationToken ct)
    {
        var pref = await repository.GetByUserIdAsync(request.UserId, ct)
                   ?? NotificationPreference.CreateDefault(request.UserId);

        return new NotificationPreferencesDto(
            pref.PushEnabled, pref.EmailEnabled,
            [
                new("class_reminder",    "Lembrete de aula",       pref.ClassReminder),
                new("class_cancellation","Cancelamento de aula",    pref.ClassCancellation),
                new("promotions",        "Promoções e ofertas",     pref.Promotions),
                new("new_modalities",    "Novas modalidades",       pref.NewModalities),
                new("payment_result",    "Resultado de pagamento",  pref.PaymentResult),
                new("referral",          "Bring a Friend",          pref.ReferralNotification)
            ]);
    }
}