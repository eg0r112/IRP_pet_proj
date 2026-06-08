namespace irp_pet.DTOs;

public class OnCallInfoDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? TelegramChatId { get; set; }
    public DateTime ShiftStartsAtUtc { get; set; }
    public DateTime ShiftEndsAtUtc { get; set; }
}

public class CreateOnCallShiftRequest
{
    public Guid UserId { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public string? Note { get; set; }
}

public class UserListItemDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? TelegramChatId { get; set; }
}
