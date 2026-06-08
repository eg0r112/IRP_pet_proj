namespace irp_pet.Models;

public class Incident
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public IncidentStatus Status { get; set; } = IncidentStatus.Open;
    public Severity Severity { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public Guid? CurrentAssigneeUserId { get; set; }
    public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public DateTime? LastAlertAtUtc { get; set; }
    public bool IsEscalated { get; set; }
    public long RowVersion { get; set; }

    public ServiceCatalog Service { get; set; } = null!;
    public User? CurrentAssignee { get; set; }
    public ICollection<Alert> Alerts { get; set; } = [];
    public ICollection<IncidentTimeline> Timeline { get; set; } = [];
}
