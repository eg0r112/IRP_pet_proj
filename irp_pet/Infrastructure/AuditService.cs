using irp_pet.Data;
using irp_pet.Models;

namespace irp_pet.Infrastructure;

public class AuditService
{
    private readonly AppDbContext _db;

    public AuditService(AppDbContext db) => _db = db;

    public async Task LogAsync(string action, string entityType, Guid? entityId, Guid? actorUserId, object? details = null, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            ActorUserId = actorUserId,
            DetailsJson = details is null ? null : System.Text.Json.JsonSerializer.Serialize(details)
        });
        await _db.SaveChangesAsync(ct);
    }
}
