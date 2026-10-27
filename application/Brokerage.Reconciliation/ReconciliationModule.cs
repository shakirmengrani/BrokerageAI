using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Brokerage.Common;

namespace Brokerage.Reconciliation;

public enum ReconciliationRunStatus { Running, Completed, CompletedWithExceptions, Failed }
public enum ReconciliationItemStatus { Matched, UnmatchedInternal, UnmatchedExternal, Mismatched }
public enum ReconciliationExceptionStatus { Open, Resolved, Waived }

public sealed class ReconciliationRun
{
    public Guid Id { get; init; }
    public required string Source { get; set; }
    public required string RunReference { get; set; }
    public ReconciliationRunStatus Status { get; set; } = ReconciliationRunStatus.Running;
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public ICollection<ReconciliationItem> Items { get; } = [];
}

public sealed class ReconciliationItem
{
    public Guid Id { get; init; }
    public Guid RunId { get; init; }
    public ReconciliationItemStatus Status { get; set; }
    public string? InternalReference { get; set; }
    public string? ExternalReference { get; set; }
    public decimal? InternalAmount { get; set; }
    public decimal? ExternalAmount { get; set; }
    public string? Currency { get; set; }
    public ReconciliationRun Run { get; init; } = null!;
    public ICollection<ReconciliationException> Exceptions { get; } = [];
}

public sealed class ReconciliationException
{
    public Guid Id { get; init; }
    public Guid ItemId { get; init; }
    public ReconciliationExceptionStatus Status { get; set; } = ReconciliationExceptionStatus.Open;
    public required string ReasonCode { get; set; }
    public required string Description { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
    public ReconciliationItem Item { get; init; } = null!;
}

public sealed class ReconciliationDb(DbContextOptions<ReconciliationDb> options) : DbContext(options)
{
    public const string Schema = "reconciliation";
    public DbSet<ReconciliationRun> Runs => Set<ReconciliationRun>();
    public DbSet<ReconciliationItem> Items => Set<ReconciliationItem>();
    public DbSet<ReconciliationException> Exceptions => Set<ReconciliationException>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<ReconciliationRun>(e =>
        {
            e.ToTable("reconciliation_runs"); e.HasKey(x => x.Id);
            e.Property(x => x.Source).HasMaxLength(64).IsRequired();
            e.Property(x => x.RunReference).HasMaxLength(128).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasIndex(x => new { x.Source, x.RunReference }).IsUnique();
        });
        modelBuilder.Entity<ReconciliationItem>(e =>
        {
            e.ToTable("reconciliation_items"); e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.InternalReference).HasMaxLength(128);
            e.Property(x => x.ExternalReference).HasMaxLength(128);
            e.Property(x => x.InternalAmount).HasPrecision(28, 8);
            e.Property(x => x.ExternalAmount).HasPrecision(28, 8);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.HasIndex(x => new { x.RunId, x.Status });
            e.HasOne(x => x.Run).WithMany(x => x.Items).HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ReconciliationException>(e =>
        {
            e.ToTable("reconciliation_exceptions"); e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.ReasonCode).HasMaxLength(64).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1024).IsRequired();
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasOne(x => x.Item).WithMany(x => x.Exceptions).HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

public static class ReconciliationModule
{
    public static IServiceCollection AddReconciliationModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ReconciliationDb>(o => o.UseNpgsql(connectionString, p => p.MigrationsHistoryTable("__EFMigrationsHistory", ReconciliationDb.Schema)));
        services.AddScoped<ReconciliationService>();
        services.AddSingleton<IJsonRpcMethodProvider, ReconciliationJsonRpcMethods>();
        return services;
    }
}
