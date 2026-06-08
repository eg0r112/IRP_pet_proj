namespace irp_pet.Infrastructure;

public sealed class TelegramNotificationPolicy
{
    private readonly IConfiguration _config;

    public TelegramNotificationPolicy(IConfiguration config) => _config = config;

    public bool IsEnabledFor(string eventType) => eventType switch
    {
        "IncidentCreated" => _config.GetValue("Telegram:NotifyOn:Created", true),
        "IncidentEscalated" => _config.GetValue("Telegram:NotifyOn:Escalated", true),
        "IncidentAcknowledged" => _config.GetValue("Telegram:NotifyOn:Acknowledged", false),
        "IncidentResolved" => _config.GetValue("Telegram:NotifyOn:Resolved", false),
        _ => false
    };
}
