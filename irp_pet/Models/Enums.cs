namespace irp_pet.Models;

public static class Roles
{
    public const string Admin = "admin";
    public const string OnCall = "oncall";
    public const string User = "user";
}

public enum IncidentStatus
{
    Open,
    Acknowledged,
    Resolved
}

public enum Severity
{
    Low,
    Medium,
    High,
    Critical
}

public enum TimelineEventType
{
    Created,
    AlertAttached,
    Acked,
    Escalated,
    Resolved,
    Commented,
    NotificationSent
}

public enum ActorType
{
    User,
    System,
    Integration
}
