using irp_pet.Models;

namespace irp_pet.DTOs;

public class IncidentListItemDto
{
    public Guid Id { get; set; }
    public string ServiceKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public IncidentStatus Status { get; set; }
    public Severity Severity { get; set; }
    public DateTime OpenedAtUtc { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public long RowVersion { get; set; }
}

public class IncidentDetailDto : IncidentListItemDto
{
    public string Fingerprint { get; set; } = string.Empty;
    public List<TimelineItemDto> Timeline { get; set; } = [];
}

public class TimelineItemDto
{
    public TimelineEventType EventType { get; set; }
    public ActorType ActorType { get; set; }
    public string? ActorEmail { get; set; }
    public string? DetailsJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class IncidentActionRequest
{
    public long RowVersion { get; set; }
}
