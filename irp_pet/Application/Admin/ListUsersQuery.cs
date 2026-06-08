using MediatR;
using Microsoft.EntityFrameworkCore;
using irp_pet.Data;
using irp_pet.DTOs;
using irp_pet.Models;

namespace irp_pet.Application.Admin;

public record ListUsersQuery : IRequest<List<UserListItemDto>>;

public class ListUsersQueryHandler : IRequestHandler<ListUsersQuery, List<UserListItemDto>>
{
    private readonly AppDbContext _db;

    public ListUsersQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<UserListItemDto>> Handle(ListUsersQuery request, CancellationToken ct) =>
        await _db.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.Email)
            .Select(u => new UserListItemDto
            {
                Id = u.Id,
                Email = u.Email,
                DisplayName = u.DisplayName,
                Role = u.Role,
                TelegramChatId = u.TelegramChatId
            })
            .ToListAsync(ct);
}
