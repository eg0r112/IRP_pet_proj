using irp_pet.Models;

namespace irp_pet.DTOs;

public class ReceiveAlertRequest
{
    public string ServiceKey { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public Severity Severity { get; set; } = Severity.High;
    public string Message { get; set; } = string.Empty;
    public string Source { get; set; } = "manual";
    public string? IdempotencyKey { get; set; }
}

public class ReceiveAlertResponse
{
    public Guid IncidentId { get; set; }
    public bool IsNewIncident { get; set; }
    public Guid AlertId { get; set; }
}
