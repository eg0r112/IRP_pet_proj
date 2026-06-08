namespace irp_pet.DTOs;

public enum IncidentActionStatus
{
    Success,
    NotFound,
    InvalidStatus,
    ConcurrencyConflict
}

public sealed record IncidentActionResult(IncidentActionStatus Status)
{
    public static IncidentActionResult Success() => new(IncidentActionStatus.Success);
    public static IncidentActionResult NotFound() => new(IncidentActionStatus.NotFound);
    public static IncidentActionResult InvalidStatus() => new(IncidentActionStatus.InvalidStatus);
    public static IncidentActionResult ConcurrencyConflict() => new(IncidentActionStatus.ConcurrencyConflict);

    public bool IsSuccess => Status == IncidentActionStatus.Success;
}
