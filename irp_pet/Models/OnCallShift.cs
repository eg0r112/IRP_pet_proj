namespace irp_pet.Models;

public class OnCallShift
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public string? Note { get; set; }

    public User User { get; set; } = null!;
}
