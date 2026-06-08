using System.Diagnostics.Metrics;

namespace irp_pet.Infrastructure;

public static class IrpMetrics
{
    public const string MeterName = "irp_pet";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> AlertsReceived =
        Meter.CreateCounter<long>("irp.alerts.received", description: "Total alerts received");

    public static readonly Counter<long> IncidentsCreated =
        Meter.CreateCounter<long>("irp.incidents.created", description: "Total new incidents created");

    public static readonly Counter<long> NotificationsSent =
        Meter.CreateCounter<long>("irp.notifications.sent", description: "Notifications successfully sent");

    public static readonly Counter<long> NotificationsFailed =
        Meter.CreateCounter<long>("irp.notifications.failed", description: "Notifications failed or skipped");

    public static readonly Counter<long> ChannelNotifications =
        Meter.CreateCounter<long>("irp.channel.notifications", description: "Notifications by channel and status");

    public static readonly Counter<long> RabbitMqPublished =
        Meter.CreateCounter<long>("irp.rabbitmq.published", description: "Events published to RabbitMQ");

    public static readonly Counter<long> RabbitMqPublishFailed =
        Meter.CreateCounter<long>("irp.rabbitmq.publish_failed", description: "Failed RabbitMQ publish attempts");

    public static readonly Counter<long> RabbitMqConsumed =
        Meter.CreateCounter<long>("irp.rabbitmq.consumed", description: "Events consumed from RabbitMQ");

    public static readonly Counter<long> OutboxProcessed =
        Meter.CreateCounter<long>("irp.outbox.processed", description: "Outbox messages processed");

    public static void RecordChannelNotification(string channel, string status)
    {
        ChannelNotifications.Add(1,
            new KeyValuePair<string, object?>("channel", channel),
            new KeyValuePair<string, object?>("status", status));

        if (status == "sent")
            NotificationsSent.Add(1);
        else
            NotificationsFailed.Add(1);
    }

    public static void RecordRabbitPublished(string eventType)
    {
        RabbitMqPublished.Add(1, new KeyValuePair<string, object?>("event_type", eventType));
    }

    public static void RecordRabbitConsumed(string eventType)
    {
        RabbitMqConsumed.Add(1, new KeyValuePair<string, object?>("event_type", eventType));
    }
}
