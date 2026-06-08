using Microsoft.EntityFrameworkCore;
using irp_pet.Data;
using irp_pet.DTOs;
using irp_pet.Infrastructure;
using irp_pet.Models;

namespace irp_pet.Services;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly AuditService _audit;

    public AuthService(AppDbContext db, JwtTokenService jwt, AuditService audit)
    {
        _db = db;
        _jwt = jwt;
        _audit = audit;
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive, ct);

        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            return null;

        var tokens = await IssueTokensAsync(user, ct);
        await _audit.LogAsync("UserLogin", "User", user.Id, user.Id, new { user.Email }, ct);
        return tokens;
    }

    public async Task<AuthResponse?> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = TokenHelper.Hash(refreshToken);
        var stored = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null || stored.RevokedAtUtc is not null || stored.ExpiresAtUtc <= DateTime.UtcNow)
            return null;

        stored.RevokedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var tokens = await IssueTokensAsync(stored.User, ct);
        await _audit.LogAsync("TokenRefreshed", "User", stored.UserId, stored.UserId, null, ct);
        return tokens;
    }

    public async Task<bool> LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = TokenHelper.Hash(refreshToken);
        var stored = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null || stored.RevokedAtUtc is not null)
            return false;

        stored.RevokedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("UserLogout", "User", stored.UserId, stored.UserId, null, ct);
        return true;
    }

    public async Task<User?> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email, ct))
            return null;

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = PasswordHasher.Hash(request.Password),
            DisplayName = request.DisplayName,
            Role = request.Role,
            TelegramChatId = request.TelegramChatId,
            IsActive = true
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("UserCreated", "User", user.Id, null, new { user.Email, user.Role }, ct);
        return user;
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken ct)
    {
        var expires = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenLifetimeMinutes);
        var access = _jwt.GenerateAccessToken(user, expires);
        var refresh = TokenHelper.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = TokenHelper.Hash(refresh),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwt.RefreshTokenLifetimeDays)
        });
        await _db.SaveChangesAsync(ct);

        return new AuthResponse
        {
            AccessToken = access,
            RefreshToken = refresh,
            AccessTokenExpiresAtUtc = expires,
            Role = user.Role
        };
    }
}
