namespace irp_pet.Models;

public class IncidentTimeline
{
    public Guid Id { get; set; }
    public Guid IncidentId { get; set; }
    public TimelineEventType EventType { get; set; }
    public ActorType ActorType { get; set; }
    public Guid? ActorId { get; set; }
    public string? DetailsJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Incident Incident { get; set; } = null!;
    public User? Actor { get; set; }
}
