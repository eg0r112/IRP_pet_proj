namespace irp_pet.Infrastructure;

public enum JiraDeliveryStatus
{
    Sent,
    Skipped,
    Failed
}

public sealed record JiraDeliveryResult(JiraDeliveryStatus Status, string? IssueKey = null, string? Error = null);

public interface IJiraService
{
    Task<JiraDeliveryResult> CreateIncidentIssueAsync(NotificationMessage message, CancellationToken ct = default);
}
