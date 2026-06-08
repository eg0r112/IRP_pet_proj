namespace irp_pet.Infrastructure;

public sealed record NotificationMessage(
    string EventType,
    Guid IncidentId,
    string? TelegramChatId,
    string ServiceKey,
    string Title,
    string Severity,
    string Status,
    string? Fingerprint,
    string? OnCallDisplayName,
    string? ActorDisplayName,
    DateTime? OpenedAtUtc,
    string? Details = null);
