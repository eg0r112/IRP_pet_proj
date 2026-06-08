using Microsoft.EntityFrameworkCore;
using irp_pet.Data;
using irp_pet.Infrastructure;
using irp_pet.Models;
namespace irp_pet.Background;

public class EscalationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EscalationWorker> _logger;

    public EscalationWorker(IServiceScopeFactory scopeFactory, ILogger<EscalationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var outbox = scope.ServiceProvider.GetRequiredService<OutboxService>();
                var threshold = DateTime.UtcNow.AddMinutes(-5);

                var stale = await db.Incidents
                    .Where(i => i.Status == IncidentStatus.Open && i.OpenedAtUtc < threshold && !i.IsEscalated)
                    .ToListAsync(stoppingToken);

                foreach (var incident in stale)
                {
                    incident.IsEscalated = true;
                    db.IncidentTimeline.Add(new IncidentTimeline
                    {
                        Id = Guid.NewGuid(),
                        IncidentId = incident.Id,
                        EventType = TimelineEventType.Escalated,
                        ActorType = ActorType.System,
                        DetailsJson = "{\"reason\":\"no_ack_within_5_min\"}"
                    });

                    await outbox.EnqueueAsync("IncidentEscalated", new
                    {
                        Id = incident.Id,
                        Title = incident.Title
                    }, stoppingToken);

                    _logger.LogWarning("Escalated incident {IncidentId}, queued notification", incident.Id);
                }

                if (stale.Count > 0)
                    await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Escalation worker error");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
