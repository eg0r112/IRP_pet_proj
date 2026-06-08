using MassTransit;
using Microsoft.EntityFrameworkCore;
using irp_pet.Data;
using irp_pet.Infrastructure;
using irp_pet.Messaging;

namespace irp_pet.Background;

public class OutboxDispatcherWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatcherWorker> _logger;

    public OutboxDispatcherWorker(IServiceScopeFactory scopeFactory, ILogger<OutboxDispatcherWorker> logger)
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
                var processor = scope.ServiceProvider.GetRequiredService<IncidentEventProcessor>();
                var publishEndpoint = scope.ServiceProvider.GetService<IPublishEndpoint>();

                var batch = await db.OutboxMessages
                    .Where(x => x.ProcessedAtUtc == null)
                    .OrderBy(x => x.OccurredAtUtc)
                    .Take(20)
                    .ToListAsync(stoppingToken);

                foreach (var msg in batch)
                {
                    try
                    {
                        if (publishEndpoint is not null)
                        {
                            await publishEndpoint.Publish(
                                new IncidentEventMessage(msg.EventType, msg.PayloadJson), stoppingToken);
                            IrpMetrics.RecordRabbitPublished(msg.EventType);
                            _logger.LogInformation("Outbox → RabbitMQ: {EventType}", msg.EventType);
                        }
                        else
                        {
                            await processor.ProcessAsync(msg.EventType, msg.PayloadJson, stoppingToken);
                            _logger.LogInformation("Outbox → локально (RabbitMQ выкл): {EventType}", msg.EventType);
                        }

                        msg.ProcessedAtUtc = DateTime.UtcNow;
                        msg.Error = null;
                        IrpMetrics.OutboxProcessed.Add(1,
                            new KeyValuePair<string, object?>("event_type", msg.EventType));
                    }
                    catch (Exception ex)
                    {
                        msg.Error = ex.Message;
                        if (publishEndpoint is not null)
                            IrpMetrics.RabbitMqPublishFailed.Add(1);
                        _logger.LogError(ex, "Outbox dispatch failed for {Id} ({EventType})", msg.Id, msg.EventType);
                    }
                }

                if (batch.Count > 0)
                    await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox worker error");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
