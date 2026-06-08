namespace irp_pet.Infrastructure;

public enum NotificationDeliveryStatus
{
    Sent,
    Skipped,
    Failed
}

public sealed record NotificationDeliveryResult(NotificationDeliveryStatus Status, string? Error = null);

public interface INotificationService
{
    Task<NotificationDeliveryResult> SendAsync(NotificationMessage message, CancellationToken ct = default);
}
