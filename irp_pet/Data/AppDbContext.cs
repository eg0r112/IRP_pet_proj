using Microsoft.EntityFrameworkCore;
using irp_pet.Models;

namespace irp_pet.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<ServiceCatalog> ServiceCatalog => Set<ServiceCatalog>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<IncidentTimeline> IncidentTimeline => Set<IncidentTimeline>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ApiClient> ApiClients => Set<ApiClient>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<OnCallShift> OnCallShifts => Set<OnCallShift>();
    public DbSet<NotificationAttempt> NotificationAttempts => Set<NotificationAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(255).IsRequired();
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(255).IsRequired();
            e.Property(x => x.Role).HasMaxLength(50).IsRequired();
            e.Property(x => x.TelegramChatId).HasMaxLength(100);
        });

        modelBuilder.Entity<ServiceCatalog>(e =>
        {
            e.ToTable("service_catalog");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(100).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(255).IsRequired();
        });

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.ClrType.IsEnum)
                    property.SetColumnType("varchar(50)");
            }
        }

        modelBuilder.Entity<Incident>(e =>
        {
            e.ToTable("incidents");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(500).IsRequired();
            e.Property(x => x.Fingerprint).HasMaxLength(255).IsRequired();
            e.Property(x => x.Status).HasConversion<string>();
            e.Property(x => x.Severity).HasConversion<string>();
            e.HasIndex(x => new { x.Fingerprint, x.Status });
            e.HasIndex(x => new { x.ServiceId, x.Status });
            e.HasOne(x => x.Service).WithMany(s => s.Incidents).HasForeignKey(x => x.ServiceId);
            e.HasOne(x => x.CurrentAssignee).WithMany().HasForeignKey(x => x.CurrentAssigneeUserId);
            e.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CreatedAtUtc);
            e.Property(x => x.Action).HasMaxLength(255).IsRequired();
            e.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.ToTable("outbox_messages");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ProcessedAtUtc);
        });

        modelBuilder.Entity<Alert>(e =>
        {
            e.ToTable("alerts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Source).HasMaxLength(100).IsRequired();
            e.Property(x => x.Fingerprint).HasMaxLength(255).IsRequired();
            e.Property(x => x.Message).IsRequired();
            e.Property(x => x.Severity).HasConversion<string>();
            e.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
            e.HasOne(x => x.Incident).WithMany(i => i.Alerts).HasForeignKey(x => x.IncidentId);
            e.HasOne(x => x.Service).WithMany(s => s.Alerts).HasForeignKey(x => x.ServiceId);
        });

        modelBuilder.Entity<IncidentTimeline>(e =>
        {
            e.ToTable("incident_timeline");
            e.HasKey(x => x.Id);
            e.Property(x => x.EventType).HasConversion<string>();
            e.Property(x => x.ActorType).HasConversion<string>();
            e.HasIndex(x => new { x.IncidentId, x.CreatedAtUtc });
            e.HasOne(x => x.Incident).WithMany(i => i.Timeline).HasForeignKey(x => x.IncidentId);
            e.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<ApiClient>(e =>
        {
            e.ToTable("api_clients");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.KeyHash).IsUnique();
            e.HasOne(x => x.AllowedService).WithMany().HasForeignKey(x => x.AllowedServiceId);
        });

        modelBuilder.Entity<OnCallShift>(e =>
        {
            e.ToTable("on_call_shifts");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.StartsAtUtc, x.EndsAtUtc });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<NotificationAttempt>(e =>
        {
            e.ToTable("notification_attempts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Channel).HasMaxLength(50).IsRequired();
            e.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => new { x.IncidentId, x.CreatedAtUtc });
            e.HasOne(x => x.Incident).WithMany().HasForeignKey(x => x.IncidentId);
        });
    }
}
