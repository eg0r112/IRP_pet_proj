namespace irp_pet.Infrastructure;

public sealed class TelegramNotificationService : INotificationService
{
    private readonly TelegramApiClient _api;
    private readonly IConfiguration _config;
    private readonly ILogger<TelegramNotificationService> _logger;

    public TelegramNotificationService(
        TelegramApiClient api,
        IConfiguration config,
        ILogger<TelegramNotificationService> logger)
    {
        _api = api;
        _config = config;
        _logger = logger;
    }

    public async Task<NotificationDeliveryResult> SendAsync(NotificationMessage message, CancellationToken ct = default)
    {
        if (!_api.IsConfigured)
        {
            _logger.LogInformation("Уведомление пропущено для {EventType} {IncidentId} (Telegram выключен)", message.EventType, message.IncidentId);
            return new NotificationDeliveryResult(NotificationDeliveryStatus.Skipped, "Telegram disabled");
        }

        if (string.IsNullOrWhiteSpace(message.TelegramChatId))
        {
            _logger.LogInformation("Уведомление пропущено для {EventType} {IncidentId} (нет chat id)", message.EventType, message.IncidentId);
            return new NotificationDeliveryResult(NotificationDeliveryStatus.Skipped, "No Telegram chat id");
        }

        var text = TelegramMessageFormatter.Format(message);
        var interactive = _config.GetValue("Telegram:InteractiveButtons", true);
        object? keyboard = interactive && message.EventType is "IncidentCreated" or "IncidentEscalated"
            ? TelegramApiClient.IncidentNotificationKeyboard(message.IncidentId)
            : null;

        try
        {
            var sent = await _api.SendMessageAsync(message.TelegramChatId, text, keyboard, ct);
            if (!sent)
                return new NotificationDeliveryResult(NotificationDeliveryStatus.Failed, "Telegram API error");

            _logger.LogInformation("Telegram отправлен: {EventType} {IncidentId}", message.EventType, message.IncidentId);
            return new NotificationDeliveryResult(NotificationDeliveryStatus.Sent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка Telegram для {EventType} {IncidentId}", message.EventType, message.IncidentId);
            return new NotificationDeliveryResult(NotificationDeliveryStatus.Failed, ex.Message);
        }
    }
}
