using Microsoft.EntityFrameworkCore;
using irp_pet.Data;
using irp_pet.DTOs;
using irp_pet.Models;

namespace irp_pet.Services;

public record OnCallInfo(Guid UserId, string Email, string DisplayName, string? TelegramChatId, DateTime ShiftStartsAtUtc, DateTime ShiftEndsAtUtc);

public class OnCallService
{
    private readonly AppDbContext _db;

    public OnCallService(AppDbContext db) => _db = db;

    public async Task<List<OnCallInfo>> GetCurrentAllAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var shifts = await _db.OnCallShifts
            .Include(s => s.User)
            .Where(s => s.StartsAtUtc <= now && s.EndsAtUtc > now && s.User.IsActive)
            .OrderByDescending(s => s.StartsAtUtc)
            .ToListAsync(ct);

        return shifts.Select(shift => new OnCallInfo(
            shift.UserId,
            shift.User.Email,
            shift.User.DisplayName,
            shift.User.TelegramChatId,
            shift.StartsAtUtc,
            shift.EndsAtUtc)).ToList();
    }

    public async Task<OnCallInfo?> GetCurrentAsync(CancellationToken ct = default)
    {
        var all = await GetCurrentAllAsync(ct);
        return all.FirstOrDefault();
    }

    public async Task<List<OnCallInfoDto>> GetCurrentAllDtoAsync(CancellationToken ct = default) =>
        (await GetCurrentAllAsync(ct)).Select(current => new OnCallInfoDto
        {
            UserId = current.UserId,
            Email = current.Email,
            DisplayName = current.DisplayName,
            TelegramChatId = current.TelegramChatId,
            ShiftStartsAtUtc = current.ShiftStartsAtUtc,
            ShiftEndsAtUtc = current.ShiftEndsAtUtc
        }).ToList();

    public async Task<OnCallInfoDto?> GetCurrentDtoAsync(CancellationToken ct = default)
    {
        var current = await GetCurrentAsync(ct);
        if (current is null)
            return null;

        return new OnCallInfoDto
        {
            UserId = current.UserId,
            Email = current.Email,
            DisplayName = current.DisplayName,
            TelegramChatId = current.TelegramChatId,
            ShiftStartsAtUtc = current.ShiftStartsAtUtc,
            ShiftEndsAtUtc = current.ShiftEndsAtUtc
        };
    }

    public async Task<OnCallShift?> CreateShiftAsync(CreateOnCallShiftRequest request, CancellationToken ct = default)
    {
        if (request.EndsAtUtc <= request.StartsAtUtc)
            return null;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && u.IsActive, ct);
        if (user is null || user.Role is not (Roles.Admin or Roles.OnCall))
            return null;

        var shift = new OnCallShift
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            StartsAtUtc = request.StartsAtUtc,
            EndsAtUtc = request.EndsAtUtc,
            Note = request.Note
        };

        _db.OnCallShifts.Add(shift);
        await _db.SaveChangesAsync(ct);
        return shift;
    }
}
