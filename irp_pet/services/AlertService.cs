using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using irp_pet.Data;
using irp_pet.DTOs;
using irp_pet.Infrastructure;
using irp_pet.Models;

namespace irp_pet.Services;

public class AlertService
{
    private readonly AppDbContext _db;
    private readonly RedisCacheService? _redis;
    private readonly OutboxService _outbox;
    private readonly AuditService _audit;
    private readonly OnCallService _onCall;

    public AlertService(
        AppDbContext db,
        OutboxService outbox,
        AuditService audit,
        OnCallService onCall,
        RedisCacheService? redis = null)
    {
        _db = db;
        _outbox = outbox;
        _audit = audit;
        _onCall = onCall;
        _redis = redis;
    }

    public async Task<ReceiveAlertResponse?> ReceiveAsync(ReceiveAlertRequest request, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(request.IdempotencyKey))
        {
            var redisKey = $"idem:{request.IdempotencyKey}";
            if (_redis is not null && await _redis.ExistsAsync(redisKey, ct))
            {
                var existing = await _db.Alerts.FirstOrDefaultAsync(a => a.IdempotencyKey == request.IdempotencyKey, ct);
                if (existing is not null)
                    return new ReceiveAlertResponse { AlertId = existing.Id, IncidentId = existing.IncidentId ?? Guid.Empty, IsNewIncident = false };
            }

            var existingAlert = await _db.Alerts
                .FirstOrDefaultAsync(a => a.IdempotencyKey == request.IdempotencyKey, ct);
            if (existingAlert is not null)
            {
                if (_redis is not null)
                    await _redis.SetAsync(redisKey, "1", TimeSpan.FromHours(24), ct);
                return new ReceiveAlertResponse
                {
                    AlertId = existingAlert.Id,
                    IncidentId = existingAlert.IncidentId ?? Guid.Empty,
                    IsNewIncident = false
                };
            }
        }

        var service = await _db.ServiceCatalog
            .FirstOrDefaultAsync(s => s.Key == request.ServiceKey && s.IsActive, ct);
        if (service is null)
            return null;

        Incident? openIncident = null;
        var dedupKey = $"dedup:{request.ServiceKey}:{request.Fingerprint}";
        if (_redis is not null && await _redis.ExistsAsync(dedupKey, ct))
        {
            var cachedIncidentId = await _redis.GetAsync(dedupKey, ct);
            if (Guid.TryParse(cachedIncidentId, out var incidentId))
            {
                openIncident = await _db.Incidents
                    .FirstOrDefaultAsync(i =>
                        i.Id == incidentId &&
                        i.ServiceId == service.Id &&
                        i.Status != IncidentStatus.Resolved, ct);
            }
        }

        openIncident ??= await _db.Incidents
            .FirstOrDefaultAsync(i =>
                i.ServiceId == service.Id &&
                i.Fingerprint == request.Fingerprint &&
                i.Status != IncidentStatus.Resolved, ct);

        var isNew = openIncident is null;
        var onCall = await _onCall.GetCurrentAsync(ct);

        if (isNew)
        {
            openIncident = new Incident
            {
                Id = Guid.NewGuid(),
                ServiceId = service.Id,
                // Краткое описание — текст алерта на русском (serviceKey хранится отдельно в ServiceId).
                Title = request.Message.Trim(),
                Status = IncidentStatus.Open,
                Severity = request.Severity,
                Fingerprint = request.Fingerprint,
                OpenedAtUtc = DateTime.UtcNow,
                LastAlertAtUtc = DateTime.UtcNow,
                CurrentAssigneeUserId = onCall?.UserId
            };
            _db.Incidents.Add(openIncident);

            _db.IncidentTimeline.Add(new IncidentTimeline
            {
                Id = Guid.NewGuid(),
                IncidentId = openIncident.Id,
                EventType = TimelineEventType.Created,
                ActorType = ActorType.Integration,
                DetailsJson = JsonSerializer.Serialize(new { request.Source, request.Message, onCallUserId = onCall?.UserId })
            });

            IrpMetrics.IncidentsCreated.Add(1);
        }
        else
        {
            openIncident!.LastAlertAtUtc = DateTime.UtcNow;
            if (request.Severity > openIncident.Severity)
                openIncident.Severity = request.Severity;
        }

        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            IncidentId = openIncident.Id,
            ServiceId = service.Id,
            Source = request.Source,
            Fingerprint = request.Fingerprint,
            Severity = request.Severity,
            Message = request.Message,
            PayloadJson = JsonSerializer.Serialize(request),
            IdempotencyKey = request.IdempotencyKey
        };
        _db.Alerts.Add(alert);

        _db.IncidentTimeline.Add(new IncidentTimeline
        {
            Id = Guid.NewGuid(),
            IncidentId = openIncident.Id,
            EventType = TimelineEventType.AlertAttached,
            ActorType = ActorType.Integration,
            DetailsJson = JsonSerializer.Serialize(new { alert.Id, request.Message })
        });

        await _db.SaveChangesAsync(ct);

        if (isNew)
        {
            await _outbox.EnqueueAsync("IncidentCreated", new
            {
                Id = openIncident.Id,
                ServiceKey = service.Key,
                Severity = request.Severity.ToString(),
                Title = openIncident.Title,
                TelegramChatId = onCall?.TelegramChatId,
                OnCallUserId = onCall?.UserId
            }, ct);
        }

        if (_redis is not null)
        {
            await _redis.SetAsync(dedupKey, openIncident.Id.ToString(), TimeSpan.FromMinutes(15), ct);
            if (!string.IsNullOrEmpty(request.IdempotencyKey))
                await _redis.SetAsync($"idem:{request.IdempotencyKey}", "1", TimeSpan.FromHours(24), ct);
        }

        await _audit.LogAsync("AlertReceived", "Incident", openIncident.Id, null, new { alert.Id, isNew, onCall?.UserId }, ct);
        IrpMetrics.AlertsReceived.Add(1);

        return new ReceiveAlertResponse
        {
            AlertId = alert.Id,
            IncidentId = openIncident.Id,
            IsNewIncident = isNew
        };
    }
}
