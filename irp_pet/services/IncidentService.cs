using AutoMapper;
using Microsoft.EntityFrameworkCore;
using irp_pet.Data;
using irp_pet.DTOs;
using irp_pet.Infrastructure;
using irp_pet.Models;

namespace irp_pet.Services;

public class IncidentService
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;
    private readonly AuditService _audit;
    private readonly OutboxService _outbox;

    public IncidentService(AppDbContext db, IMapper mapper, AuditService audit, OutboxService outbox)
    {
        _db = db;
        _mapper = mapper;
        _audit = audit;
        _outbox = outbox;
    }

    public async Task<List<IncidentListItemDto>> GetAllAsync(IncidentStatus? status, CancellationToken ct = default)
    {
        var query = _db.Incidents.Include(i => i.Service).AsQueryable();
        if (status.HasValue)
            query = query.Where(i => i.Status == status.Value);

        var items = await query.OrderByDescending(i => i.OpenedAtUtc).ToListAsync(ct);
        return _mapper.Map<List<IncidentListItemDto>>(items);
    }

    public async Task<IncidentListV2Response> GetPagedAsync(
        IncidentStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Incidents.Include(i => i.Service).AsQueryable();
        if (status.HasValue)
            query = query.Where(i => i.Status == status.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(i => i.OpenedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new IncidentListV2Response
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize),
            Items = _mapper.Map<List<IncidentListItemDto>>(items)
        };
    }

    public async Task<IncidentDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var incident = await _db.Incidents
            .Include(i => i.Service)
            .Include(i => i.Timeline)
                .ThenInclude(t => t.Actor)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        return incident is null ? null : _mapper.Map<IncidentDetailDto>(incident);
    }

    public async Task<IncidentActionResult> AcknowledgeAsync(Guid id, Guid userId, long rowVersion, CancellationToken ct = default)
    {
        var incident = await _db.Incidents.FindAsync([id], ct);
        if (incident is null)
            return IncidentActionResult.NotFound();
        if (incident.Status != IncidentStatus.Open)
            return IncidentActionResult.InvalidStatus();
        if (incident.RowVersion != rowVersion)
            return IncidentActionResult.ConcurrencyConflict();

        incident.Status = IncidentStatus.Acknowledged;
        incident.AcknowledgedAtUtc = DateTime.UtcNow;
        incident.CurrentAssigneeUserId = userId;
        incident.RowVersion++;

        _db.IncidentTimeline.Add(new IncidentTimeline
        {
            Id = Guid.NewGuid(),
            IncidentId = id,
            EventType = TimelineEventType.Acked,
            ActorType = ActorType.User,
            ActorId = userId
        });

        try
        {
            await _db.SaveChangesAsync(ct);
            await _audit.LogAsync("IncidentAcknowledged", "Incident", id, userId, new { rowVersion }, ct);
            await _outbox.EnqueueAsync("IncidentAcknowledged", new { Id = id, UserId = userId }, ct);
            return IncidentActionResult.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return IncidentActionResult.ConcurrencyConflict();
        }
    }

    public async Task<IncidentActionResult> ResolveAsync(Guid id, Guid userId, long rowVersion, CancellationToken ct = default)
    {
        var incident = await _db.Incidents.FindAsync([id], ct);
        if (incident is null)
            return IncidentActionResult.NotFound();
        if (incident.Status == IncidentStatus.Resolved)
            return IncidentActionResult.InvalidStatus();
        if (incident.RowVersion != rowVersion)
            return IncidentActionResult.ConcurrencyConflict();

        incident.Status = IncidentStatus.Resolved;
        incident.ResolvedAtUtc = DateTime.UtcNow;
        incident.RowVersion++;

        _db.IncidentTimeline.Add(new IncidentTimeline
        {
            Id = Guid.NewGuid(),
            IncidentId = id,
            EventType = TimelineEventType.Resolved,
            ActorType = ActorType.User,
            ActorId = userId
        });

        try
        {
            await _db.SaveChangesAsync(ct);
            await _audit.LogAsync("IncidentResolved", "Incident", id, userId, new { rowVersion }, ct);
            await _outbox.EnqueueAsync("IncidentResolved", new { Id = id, UserId = userId }, ct);
            return IncidentActionResult.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return IncidentActionResult.ConcurrencyConflict();
        }
    }
}
