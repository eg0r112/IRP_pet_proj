namespace irp_pet.Models;

public class Alert
{
    public Guid Id { get; set; }
    public Guid? IncidentId { get; set; }
    public Guid ServiceId { get; set; }
    public string Source { get; set; } = "manual";
    public string Fingerprint { get; set; } = string.Empty;
    public Severity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public string? IdempotencyKey { get; set; }

    public Incident? Incident { get; set; }
    public ServiceCatalog Service { get; set; } = null!;
}
