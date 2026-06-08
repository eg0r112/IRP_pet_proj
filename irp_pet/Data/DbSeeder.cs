using Microsoft.EntityFrameworkCore;
using irp_pet.Models;
using irp_pet.Services;

namespace irp_pet.Data;

public static class DbSeeder
{
    private static readonly Guid AdminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static async Task SeedAsync(AppDbContext db, IConfiguration config)
    {
        if (!await db.Users.AnyAsync())
        {
            db.Users.Add(new User
            {
                Id = AdminUserId,
                Email = "vedromakaron@gmail.com",
                PasswordHash = PasswordHasher.Hash("123456"),
                DisplayName = "Egor Makarov",
                Role = Roles.Admin,
                TelegramChatId = "5143241640",
                IsActive = true
            });

            db.Users.Add(new User
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Email = "oncall@irp.local",
                PasswordHash = PasswordHasher.Hash("123456"),
                DisplayName = "On-Call Engineer",
                Role = Roles.OnCall,
                TelegramChatId = "5143241640",
                IsActive = true
            });
        }
        else if (!await db.Users.AnyAsync(u => u.Email == "oncall@irp.local"))
        {
            db.Users.Add(new User
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Email = "oncall@irp.local",
                PasswordHash = PasswordHasher.Hash("123456"),
                DisplayName = "On-Call Engineer",
                Role = Roles.OnCall,
                TelegramChatId = "5143241640",
                IsActive = true
            });
        }

        if (!await db.ServiceCatalog.AnyAsync())
        {
            db.ServiceCatalog.AddRange(
                new ServiceCatalog
                {
                    Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    Key = "orders-api",
                    DisplayName = "Orders API"
                },
                new ServiceCatalog
                {
                    Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    Key = "payments-api",
                    DisplayName = "Payments API"
                });
        }

        var apiKey = config["ApiKey:Value"] ?? "dev-api-key-change-me";
        if (!await db.ApiClients.AnyAsync())
        {
            db.ApiClients.Add(new ApiClient
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                Name = "monitoring-default",
                KeyHash = TokenHelper.Hash(apiKey),
                IsActive = true
            });
        }

        if (!await db.OnCallShifts.AnyAsync())
        {
            var now = DateTime.UtcNow;
            var onCallUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            db.OnCallShifts.AddRange(
                new OnCallShift
                {
                    Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                    UserId = AdminUserId,
                    StartsAtUtc = now.AddDays(-1),
                    EndsAtUtc = now.AddDays(30),
                    Note = "Default admin on-call shift (seed)"
                },
                new OnCallShift
                {
                    Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                    UserId = onCallUserId,
                    StartsAtUtc = now.AddDays(-1),
                    EndsAtUtc = now.AddDays(30),
                    Note = "Default on-call engineer shift (seed)"
                });
        }

        await SeedDemoIncidentsAsync(db);

        await db.SaveChangesAsync();
    }

    private static readonly Guid OrdersServiceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PaymentsServiceId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid OnCallUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static async Task SeedDemoIncidentsAsync(AppDbContext db)
    {
        if (await db.Incidents.AnyAsync())
            return;

        var now = DateTime.UtcNow;

        var incidents = new[]
        {
            new Incident
            {
                Id = Guid.Parse("f0010001-0001-0001-0001-000000000001"),
                ServiceId = OrdersServiceId,
                Title = "Сервис не отвечает",
                Status = IncidentStatus.Open,
                Severity = Severity.Critical,
                Fingerprint = "demo-orders-down",
                OpenedAtUtc = now.AddMinutes(-8),
                LastAlertAtUtc = now.AddMinutes(-8)
            },
            new Incident
            {
                Id = Guid.Parse("f0010001-0001-0001-0001-000000000002"),
                ServiceId = PaymentsServiceId,
                Title = "Ошибки при оплате",
                Status = IncidentStatus.Open,
                Severity = Severity.High,
                Fingerprint = "demo-payments-errors",
                OpenedAtUtc = now.AddMinutes(-35),
                LastAlertAtUtc = now.AddMinutes(-35)
            },
            new Incident
            {
                Id = Guid.Parse("f0010001-0001-0001-0001-000000000003"),
                ServiceId = OrdersServiceId,
                Title = "Медленные запросы к БД",
                Status = IncidentStatus.Acknowledged,
                Severity = Severity.Medium,
                Fingerprint = "demo-orders-db-slow",
                CurrentAssigneeUserId = OnCallUserId,
                OpenedAtUtc = now.AddHours(-2),
                AcknowledgedAtUtc = now.AddHours(-1),
                LastAlertAtUtc = now.AddHours(-2)
            },
            new Incident
            {
                Id = Guid.Parse("f0010001-0001-0001-0001-000000000004"),
                ServiceId = PaymentsServiceId,
                Title = "Webhook'и приходят с задержкой",
                Status = IncidentStatus.Acknowledged,
                Severity = Severity.Low,
                Fingerprint = "demo-payments-webhook-lag",
                CurrentAssigneeUserId = AdminUserId,
                OpenedAtUtc = now.AddHours(-5),
                AcknowledgedAtUtc = now.AddHours(-4),
                LastAlertAtUtc = now.AddHours(-5)
            },
            new Incident
            {
                Id = Guid.Parse("f0010001-0001-0001-0001-000000000005"),
                ServiceId = OrdersServiceId,
                Title = "Починили отправку писем",
                Status = IncidentStatus.Resolved,
                Severity = Severity.Medium,
                Fingerprint = "demo-orders-mail",
                CurrentAssigneeUserId = OnCallUserId,
                OpenedAtUtc = now.AddDays(-1),
                AcknowledgedAtUtc = now.AddDays(-1).AddHours(1),
                ResolvedAtUtc = now.AddHours(-6),
                LastAlertAtUtc = now.AddDays(-1)
            }
        };

        db.Incidents.AddRange(incidents);

        foreach (var incident in incidents)
        {
            db.Alerts.Add(new Alert
            {
                Id = Guid.NewGuid(),
                IncidentId = incident.Id,
                ServiceId = incident.ServiceId,
                Source = "demo",
                Fingerprint = incident.Fingerprint,
                Severity = incident.Severity,
                Message = incident.Title,
                ReceivedAtUtc = incident.OpenedAtUtc
            });

            db.IncidentTimeline.Add(new IncidentTimeline
            {
                Id = Guid.NewGuid(),
                IncidentId = incident.Id,
                EventType = TimelineEventType.Created,
                ActorType = ActorType.Integration,
                CreatedAtUtc = incident.OpenedAtUtc
            });

            if (incident.Status is IncidentStatus.Acknowledged or IncidentStatus.Resolved)
            {
                db.IncidentTimeline.Add(new IncidentTimeline
                {
                    Id = Guid.NewGuid(),
                    IncidentId = incident.Id,
                    EventType = TimelineEventType.Acked,
                    ActorType = ActorType.User,
                    ActorId = incident.CurrentAssigneeUserId,
                    CreatedAtUtc = incident.AcknowledgedAtUtc ?? incident.OpenedAtUtc
                });
            }

            if (incident.Status == IncidentStatus.Resolved)
            {
                db.IncidentTimeline.Add(new IncidentTimeline
                {
                    Id = Guid.NewGuid(),
                    IncidentId = incident.Id,
                    EventType = TimelineEventType.Resolved,
                    ActorType = ActorType.User,
                    ActorId = incident.CurrentAssigneeUserId,
                    CreatedAtUtc = incident.ResolvedAtUtc ?? now
                });
            }
        }
    }
}
