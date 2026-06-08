using irp_pet.Data;
using irp_pet.Models;

namespace irp_pet.Infrastructure;

public class OutboxService
{
    private readonly AppDbContext _db;

    public OutboxService(AppDbContext db) => _db = db;

    public async Task EnqueueAsync(string eventType, object payload, CancellationToken ct = default)
    {
        _db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(payload)
        });
        await _db.SaveChangesAsync(ct);
    }
}
