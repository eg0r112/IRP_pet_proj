namespace irp_pet.Models;

public enum NotificationStatus
{
    Sent,
    Skipped,
    Failed
}

public class NotificationAttempt
{
    public Guid Id { get; set; }
    public Guid IncidentId { get; set; }
    public string Channel { get; set; } = "telegram";
    public string EventType { get; set; } = string.Empty;
    public NotificationStatus Status { get; set; }
    public string? Target { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Incident Incident { get; set; } = null!;
}
