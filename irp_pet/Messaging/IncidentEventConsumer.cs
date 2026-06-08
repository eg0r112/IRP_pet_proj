using MassTransit;
using irp_pet.Infrastructure;

namespace irp_pet.Messaging;

/// <summary>
/// Основная обработка событий: Telegram, Jira, timeline, notification_attempts.
/// OutboxDispatcher только публикует в RabbitMQ — consumer делает всю работу.
/// </summary>
public class IncidentEventConsumer : IConsumer<IncidentEventMessage>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IncidentEventConsumer> _logger;

    public IncidentEventConsumer(IServiceScopeFactory scopeFactory, ILogger<IncidentEventConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<IncidentEventMessage> context)
    {
        var message = context.Message;
        IrpMetrics.RecordRabbitConsumed(message.EventType);

        using var scope = _scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IncidentEventProcessor>();

        await processor.ProcessAsync(message.EventType, message.PayloadJson, context.CancellationToken);

        _logger.LogInformation(
            "RabbitMQ consumer обработал {EventType} (TG, Jira, timeline)",
            message.EventType);
    }
}
