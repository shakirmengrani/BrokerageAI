using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Brokerage.Common;

namespace Brokerage.Reporting;

public enum ReportJobStatus { Queued, Running, Completed, Failed, Cancelled }

public sealed class AuditEvent
{
    public Guid Id { get; init; }
    public required string ActorId { get; set; }
    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? CorrelationId { get; set; }
    public string? PayloadJson { get; set; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class ReportJob
{
    public Guid Id { get; init; }
    public required string ReportType { get; set; }
    public required string RequestedBy { get; set; }
    public ReportJobStatus Status { get; set; } = ReportJobStatus.Queued;
    public string? ParametersJson { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ErrorCode { get; set; }
    public ICollection<ReportArtifact> Artifacts { get; } = [];
}

public sealed class ReportArtifact
{
    public Guid Id { get; init; }
    public Guid ReportJobId { get; init; }
    public required string StorageKey { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public ReportJob ReportJob { get; init; } = null!;
}

public sealed class ReportingDb(DbContextOptions<ReportingDb> options) : DbContext(options)
{
    public const string Schema = "reporting";
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<ReportJob> ReportJobs => Set<ReportJob>();
    public DbSet<ReportArtifact> ReportArtifacts => Set<ReportArtifact>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<AuditEvent>(e =>
        {
            e.ToTable("audit_events"); e.HasKey(x => x.Id);
            e.Property(x => x.ActorId).HasMaxLength(128).IsRequired();
            e.Property(x => x.Action).HasMaxLength(128).IsRequired();
            e.Property(x => x.EntityType).HasMaxLength(128).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(128);
            e.Property(x => x.CorrelationId).HasMaxLength(128);
            e.Property(x => x.PayloadJson).HasColumnType("jsonb");
            e.HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredAt });
            e.HasIndex(x => x.OccurredAt);
        });
        modelBuilder.Entity<ReportJob>(e =>
        {
            e.ToTable("report_jobs"); e.HasKey(x => x.Id);
            e.Property(x => x.ReportType).HasMaxLength(64).IsRequired();
            e.Property(x => x.RequestedBy).HasMaxLength(128).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.ParametersJson).HasColumnType("jsonb");
            e.Property(x => x.ErrorCode).HasMaxLength(64);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });
        modelBuilder.Entity<ReportArtifact>(e =>
        {
            e.ToTable("report_artifacts"); e.HasKey(x => x.Id);
            e.Property(x => x.StorageKey).HasMaxLength(512).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.StorageKey).IsUnique();
            e.HasOne(x => x.ReportJob).WithMany(x => x.Artifacts).HasForeignKey(x => x.ReportJobId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

public static class ReportingModule
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ReportingDb>(o => o.UseNpgsql(connectionString, p => p.MigrationsHistoryTable("__EFMigrationsHistory", ReportingDb.Schema)));
        services.AddScoped<ReportingService>();
        services.AddSingleton<IJsonRpcMethodProvider, ReportingJsonRpcMethods>();
        return services;
    }
}
