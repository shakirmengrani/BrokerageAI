using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Brokerage.Common;

namespace Brokerage.Admin;

public sealed class TenantSetting
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public required string Key { get; set; }
    public required string ValueJson { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
}

public sealed class OperationalControl
{
    public Guid Id { get; init; }
    public required string ControlKey { get; set; }
    public bool IsEnabled { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
}

public sealed class AdminAuditEvent
{
    public Guid Id { get; init; }
    public required string ActorId { get; set; }
    public required string Action { get; set; }
    public required string TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? DetailsJson { get; set; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class AdminDb(DbContextOptions<AdminDb> options) : DbContext(options)
{
    public const string Schema = "admin";
    public DbSet<TenantSetting> TenantSettings => Set<TenantSetting>();
    public DbSet<OperationalControl> OperationalControls => Set<OperationalControl>();
    public DbSet<AdminAuditEvent> AuditEvents => Set<AdminAuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<TenantSetting>(e =>
        {
            e.ToTable("tenant_settings"); e.HasKey(x => x.Id);
            e.Property(x => x.Key).HasMaxLength(128).IsRequired();
            e.Property(x => x.ValueJson).HasColumnType("jsonb").IsRequired();
            e.Property(x => x.UpdatedBy).HasMaxLength(128);
            e.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
        });
        modelBuilder.Entity<OperationalControl>(e =>
        {
            e.ToTable("operational_controls"); e.HasKey(x => x.Id);
            e.Property(x => x.ControlKey).HasMaxLength(128).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(1024);
            e.Property(x => x.UpdatedBy).HasMaxLength(128);
            e.HasIndex(x => x.ControlKey).IsUnique();
        });
        modelBuilder.Entity<AdminAuditEvent>(e =>
        {
            e.ToTable("admin_audit_events"); e.HasKey(x => x.Id);
            e.Property(x => x.ActorId).HasMaxLength(128).IsRequired();
            e.Property(x => x.Action).HasMaxLength(128).IsRequired();
            e.Property(x => x.TargetType).HasMaxLength(128).IsRequired();
            e.Property(x => x.TargetId).HasMaxLength(128);
            e.Property(x => x.DetailsJson).HasColumnType("jsonb");
            e.HasIndex(x => x.OccurredAt);
            e.HasIndex(x => new { x.TargetType, x.TargetId, x.OccurredAt });
        });
    }
}

public static class AdminModule
{
    public static IServiceCollection AddAdminModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AdminDb>(o => o.UseNpgsql(connectionString, p => p.MigrationsHistoryTable("__EFMigrationsHistory", AdminDb.Schema)));
        services.AddScoped<AdminService>();
        services.AddSingleton<IJsonRpcMethodProvider, AdminJsonRpcMethods>();
        return services;
    }
}
