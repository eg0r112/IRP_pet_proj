using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using irp_pet.Data;
using irp_pet.Models;
using irp_pet.Services;

namespace irp_pet.Infrastructure;

public class IncidentEventProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IJiraService _jira;
    private readonly TelegramNotificationPolicy _policy;
    private readonly OnCallService _onCall;
    private readonly ILogger<IncidentEventProcessor> _logger;

    public IncidentEventProcessor(
        AppDbContext db,
        INotificationService notifications,
        IJiraService jira,
        TelegramNotificationPolicy policy,
        OnCallService onCall,
        ILogger<IncidentEventProcessor> logger)
    {
        _db = db;
        _notifications = notifications;
        _jira = jira;
        _policy = policy;
        _onCall = onCall;
        _logger = logger;
    }

    public async Task ProcessAsync(string eventType, string payloadJson, CancellationToken ct = default)
    {
        _logger.LogInformation("Обработка события {EventType}", eventType);

        switch (eventType)
        {
            case "IncidentCreated":
                await HandleIncidentCreatedAsync(payloadJson, ct);
                break;
            case "IncidentEscalated":
                await HandleIncidentEscalatedAsync(payloadJson, ct);
                break;
            case "IncidentAcknowledged":
                await HandleActionEventAsync("IncidentAcknowledged", payloadJson, null, ct);
                break;
            case "IncidentResolved":
                await HandleActionEventAsync("IncidentResolved", payloadJson, null, ct);
                break;
            default:
                _logger.LogWarning("Неизвестный тип события {EventType}", eventType);
                break;
        }
    }

    private async Task HandleIncidentCreatedAsync(string payloadJson, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<IncidentCreatedPayload>(payloadJson, JsonOptions);
        if (payload is null)
            return;

        var incident = await LoadIncidentAsync(payload.Id, ct);
        if (incident is null)
            return;

        var onCalls = await _onCall.GetCurrentAllAsync(ct);
        var onCallNames = string.Join(", ", onCalls.Select(o => o.DisplayName));

        var notification = BuildMessage(
            "IncidentCreated",
            incident,
            chatId: null,
            onCallNames.Length > 0 ? onCallNames : null,
            actorName: null,
            "Требуется подтверждение (ack) от дежурного.");

        await DeliverToAllOnCallAsync(notification, ct);
        await DeliverJiraAsync(notification, ct);
    }

    private async Task HandleIncidentEscalatedAsync(string payloadJson, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<IncidentEscalatedPayload>(payloadJson, JsonOptions);
        if (payload is null)
            return;

        var incident = await LoadIncidentAsync(payload.Id, ct);
        if (incident is null)
            return;

        var onCalls = await _onCall.GetCurrentAllAsync(ct);
        var onCallNames = string.Join(", ", onCalls.Select(o => o.DisplayName));

        await DeliverToAllOnCallAsync(BuildMessage(
            "IncidentEscalated",
            incident,
            chatId: null,
            onCallNames.Length > 0 ? onCallNames : null,
            actorName: null,
            "Инцидент открыт более 5 минут без подтверждения."), ct);
    }

    private async Task HandleActionEventAsync(string eventType, string payloadJson, string? details, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<IncidentActionPayload>(payloadJson, JsonOptions);
        if (payload is null)
            return;

        var incident = await LoadIncidentAsync(payload.Id, ct);
        if (incident is null)
            return;

        string? actorName = null;
        if (payload.UserId.HasValue)
        {
            var user = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == payload.UserId.Value, ct);
            actorName = user?.DisplayName ?? user?.Email;
        }

        var onCalls = await _onCall.GetCurrentAllAsync(ct);
        var onCallNames = string.Join(", ", onCalls.Select(o => o.DisplayName));
        await DeliverToAllOnCallAsync(
            BuildMessage(eventType, incident, chatId: null, onCallNames.Length > 0 ? onCallNames : null, actorName, details), ct);
    }

    private async Task<Incident?> LoadIncidentAsync(Guid id, CancellationToken ct) =>
        await _db.Incidents
            .Include(i => i.Service)
            .Include(i => i.CurrentAssignee)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, ct);

    private static NotificationMessage BuildMessage(
        string eventType,
        Incident incident,
        string? chatId,
        string? onCallName,
        string? actorName,
        string? details) =>
        new(
            eventType,
            incident.Id,
            chatId,
            incident.Service.Key,
            incident.Title,
            incident.Severity.ToString(),
            incident.Status.ToString(),
            incident.Fingerprint,
            onCallName ?? incident.CurrentAssignee?.DisplayName,
            actorName,
            incident.OpenedAtUtc,
            details);

    private async Task DeliverToAllOnCallAsync(NotificationMessage template, CancellationToken ct)
    {
        var onCalls = await _onCall.GetCurrentAllAsync(ct);
        var targets = onCalls
            .Where(o => !string.IsNullOrWhiteSpace(o.TelegramChatId))
            .GroupBy(o => o.TelegramChatId!)
            .Select(g => g.First())
            .ToList();

        if (targets.Count == 0)
        {
            await DeliverAsync(template, ct);
            return;
        }

        foreach (var onCall in targets)
        {
            await DeliverAsync(template with
            {
                TelegramChatId = onCall.TelegramChatId,
                OnCallDisplayName = onCall.DisplayName
            }, ct);
        }
    }

    private async Task DeliverAsync(NotificationMessage message, CancellationToken ct)
    {
        NotificationDeliveryResult result;

        if (!_policy.IsEnabledFor(message.EventType))
        {
            _logger.LogInformation(
                "TG пропущен: {EventType} для {IncidentId} (выключено в настройках)",
                message.EventType, message.IncidentId);
            result = new NotificationDeliveryResult(NotificationDeliveryStatus.Skipped, "Disabled in config");
        }
        else if (await AlreadySentAsync(message.IncidentId, message.EventType, "telegram", message.TelegramChatId, ct))
        {
            _logger.LogInformation(
                "TG пропущен: {EventType} для {IncidentId} (уже отправляли)",
                message.EventType, message.IncidentId);
            result = new NotificationDeliveryResult(NotificationDeliveryStatus.Skipped, "Already sent");
        }
        else
        {
            result = await _notifications.SendAsync(message, ct);
        }

        var status = result.Status switch
        {
            NotificationDeliveryStatus.Sent => NotificationStatus.Sent,
            NotificationDeliveryStatus.Skipped => NotificationStatus.Skipped,
            _ => NotificationStatus.Failed
        };

        _db.NotificationAttempts.Add(new NotificationAttempt
        {
            Id = Guid.NewGuid(),
            IncidentId = message.IncidentId,
            Channel = "telegram",
            EventType = message.EventType,
            Status = status,
            Target = message.TelegramChatId,
            Error = result.Error
        });

        _db.IncidentTimeline.Add(new IncidentTimeline
        {
            Id = Guid.NewGuid(),
            IncidentId = message.IncidentId,
            EventType = TimelineEventType.NotificationSent,
            ActorType = ActorType.System,
            DetailsJson = JsonSerializer.Serialize(new
            {
                eventType = message.EventType,
                channel = "telegram",
                status = status.ToString(),
                severity = message.Severity,
                target = message.TelegramChatId,
                error = result.Error
            })
        });

        await _db.SaveChangesAsync(ct);

        IrpMetrics.RecordChannelNotification("telegram", status switch
        {
            NotificationStatus.Sent => "sent",
            NotificationStatus.Skipped => "skipped",
            _ => "failed"
        });
    }

    private async Task DeliverJiraAsync(NotificationMessage message, CancellationToken ct)
    {
        if (await AlreadySentAsync(message.IncidentId, message.EventType, "jira", null, ct))
        {
            _logger.LogInformation("Jira пропущен: {EventType} для {IncidentId} (уже создавали)",
                message.EventType, message.IncidentId);
            IrpMetrics.RecordChannelNotification("jira", "skipped");
            return;
        }

        var result = await _jira.CreateIncidentIssueAsync(message, ct);

        var status = result.Status switch
        {
            JiraDeliveryStatus.Sent => NotificationStatus.Sent,
            JiraDeliveryStatus.Skipped => NotificationStatus.Skipped,
            _ => NotificationStatus.Failed
        };

        _db.NotificationAttempts.Add(new NotificationAttempt
        {
            Id = Guid.NewGuid(),
            IncidentId = message.IncidentId,
            Channel = "jira",
            EventType = message.EventType,
            Status = status,
            Target = result.IssueKey,
            Error = result.Error
        });

        _db.IncidentTimeline.Add(new IncidentTimeline
        {
            Id = Guid.NewGuid(),
            IncidentId = message.IncidentId,
            EventType = TimelineEventType.NotificationSent,
            ActorType = ActorType.System,
            DetailsJson = JsonSerializer.Serialize(new
            {
                eventType = message.EventType,
                channel = "jira",
                status = status.ToString(),
                issueKey = result.IssueKey,
                error = result.Error
            })
        });

        await _db.SaveChangesAsync(ct);

        IrpMetrics.RecordChannelNotification("jira", status switch
        {
            NotificationStatus.Sent => "sent",
            NotificationStatus.Skipped => "skipped",
            _ => "failed"
        });
    }

    private Task<bool> AlreadySentAsync(
        Guid incidentId, string eventType, string channel, string? target, CancellationToken ct) =>
        _db.NotificationAttempts.AnyAsync(
            x => x.IncidentId == incidentId
                 && x.EventType == eventType
                 && x.Channel == channel
                 && x.Target == target
                 && x.Status == NotificationStatus.Sent,
            ct);

    private sealed record IncidentCreatedPayload(Guid Id, string ServiceKey, string Severity, string? Title, string? TelegramChatId);
    private sealed record IncidentEscalatedPayload(Guid Id, string Title, string? TelegramChatId);
    private sealed record IncidentActionPayload(Guid Id, Guid? UserId);
}
