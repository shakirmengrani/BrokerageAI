using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Brokerage.Common;

namespace Brokerage.Notifications;

public enum NotificationStatus { Queued, Sending, Sent, Failed, Cancelled }
public enum DeliveryAttemptStatus { Succeeded, Failed }

public sealed class Notification
{
    public Guid Id { get; init; }
    public Guid? ClientId { get; set; }
    public required string Channel { get; set; }
    public required string Destination { get; set; }
    public required string Template { get; set; }
    public string? PayloadJson { get; set; }
    public NotificationStatus Status { get; set; } = NotificationStatus.Queued;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
    public ICollection<DeliveryAttempt> Attempts { get; } = [];
}

public sealed class DeliveryAttempt
{
    public Guid Id { get; init; }
    public Guid NotificationId { get; init; }
    public DeliveryAttemptStatus Status { get; set; }
    public string? ProviderReference { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset AttemptedAt { get; init; } = DateTimeOffset.UtcNow;
    public Notification Notification { get; init; } = null!;
}

public sealed class NotificationPreference
{
    public Guid Id { get; init; }
    public Guid ClientId { get; init; }
    public required string Channel { get; set; }
    public required string Category { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class NotificationsDb(DbContextOptions<NotificationsDb> options) : DbContext(options)
{
    public const string Schema = "notifications";
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<DeliveryAttempt> DeliveryAttempts => Set<DeliveryAttempt>();
    public DbSet<NotificationPreference> Preferences => Set<NotificationPreference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Notification>(e =>
        {
            e.ToTable("notifications"); e.HasKey(x => x.Id);
            e.Property(x => x.Channel).HasMaxLength(32).IsRequired();
            e.Property(x => x.Destination).HasMaxLength(512).IsRequired();
            e.Property(x => x.Template).HasMaxLength(128).IsRequired();
            e.Property(x => x.PayloadJson).HasColumnType("jsonb");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });
        modelBuilder.Entity<DeliveryAttempt>(e =>
        {
            e.ToTable("delivery_attempts"); e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.ProviderReference).HasMaxLength(128);
            e.Property(x => x.ErrorCode).HasMaxLength(64);
            e.HasIndex(x => new { x.NotificationId, x.AttemptedAt });
            e.HasOne(x => x.Notification).WithMany(x => x.Attempts).HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<NotificationPreference>(e =>
        {
            e.ToTable("notification_preferences"); e.HasKey(x => x.Id);
            e.Property(x => x.Channel).HasMaxLength(32).IsRequired();
            e.Property(x => x.Category).HasMaxLength(64).IsRequired();
            e.HasIndex(x => new { x.ClientId, x.Channel, x.Category }).IsUnique();
        });
    }
}

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<NotificationsDb>(o => o.UseNpgsql(connectionString, p => p.MigrationsHistoryTable("__EFMigrationsHistory", NotificationsDb.Schema)));
        services.AddScoped<NotificationsService>();
        services.AddSingleton<IJsonRpcMethodProvider, NotificationsJsonRpcMethods>();
        return services;
    }
}
