namespace irp_pet.Models;

public class ServiceCatalog
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? OwnerTeam { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Incident> Incidents { get; set; } = [];
    public ICollection<Alert> Alerts { get; set; } = [];
}
