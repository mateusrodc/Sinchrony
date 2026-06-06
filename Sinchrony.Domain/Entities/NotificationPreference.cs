namespace Sinchrony.Domain.Entities;

public class NotificationPreference
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public bool PushEnabled { get; private set; } = true;
    public bool EmailEnabled { get; private set; } = true;
    public bool ClassReminder { get; private set; } = true;
    public bool ClassCancellation { get; private set; } = true;
    public bool Promotions { get; private set; }
    public bool NewModalities { get; private set; } = true;
    public bool PaymentResult { get; private set; } = true;
    public bool ReferralNotification { get; private set; }

    public User? User { get; private set; }

    protected NotificationPreference() { }

    public static NotificationPreference CreateDefault(Guid userId) => new() { UserId = userId };

    public void UpdatePreference(string id, bool enabled)
    {
        switch (id)
        {
            case "class_reminder": ClassReminder = enabled; break;
            case "class_cancellation": ClassCancellation = enabled; break;
            case "promotions": Promotions = enabled; break;
            case "new_modalities": NewModalities = enabled; break;
            case "payment_result": PaymentResult = enabled; break;
            case "referral": ReferralNotification = enabled; break;
        }
    }

    public void SetPush(bool enabled) => PushEnabled = enabled;
    public void SetEmail(bool enabled) => EmailEnabled = enabled;
}