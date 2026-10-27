using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Brokerage.Common;

namespace Brokerage.Settlement;

public enum SettlementStatus { Pending, Settled, Failed, Cancelled }
public enum SettlementDirection { Receive, Deliver }
public enum SettlementFailureStatus { Open, Resolved, Waived }

public sealed class SettlementBatch
{
    public Guid Id { get; init; }
    public required string BatchReference { get; set; }
    public DateOnly SettlementDate { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public ICollection<SettlementInstruction> Instructions { get; } = [];
}

public sealed class SettlementInstruction
{
    public Guid Id { get; init; }
    public Guid BatchId { get; init; }
    public Guid AccountId { get; init; }
    public Guid? OrderId { get; set; }
    public Guid InstrumentId { get; init; }
    public SettlementDirection Direction { get; set; }
    public SettlementStatus Status { get; set; } = SettlementStatus.Pending;
    public decimal Quantity { get; set; }
    public decimal CashAmount { get; set; }
    public required string Currency { get; set; }
    public DateOnly SettlementDate { get; set; }
    public required string ExternalReference { get; set; }
    public SettlementBatch Batch { get; init; } = null!;
    public ICollection<SettlementFailure> Failures { get; } = [];
}

public sealed class SettlementFailure
{
    public Guid Id { get; init; }
    public Guid InstructionId { get; init; }
    public SettlementFailureStatus Status { get; set; } = SettlementFailureStatus.Open;
    public required string ReasonCode { get; set; }
    public required string Description { get; set; }
    public DateTimeOffset DetectedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
    public SettlementInstruction Instruction { get; init; } = null!;
}

public sealed class SettlementDb(DbContextOptions<SettlementDb> options) : DbContext(options)
{
    public const string Schema = "settlement";
    public DbSet<SettlementInstruction> Instructions => Set<SettlementInstruction>();
    public DbSet<SettlementBatch> Batches => Set<SettlementBatch>();
    public DbSet<SettlementFailure> Failures => Set<SettlementFailure>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<SettlementBatch>(e =>
        {
            e.ToTable("settlement_batches"); e.HasKey(x => x.Id);
            e.Property(x => x.BatchReference).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.BatchReference).IsUnique();
        });
        modelBuilder.Entity<SettlementInstruction>(e =>
        {
            e.ToTable("settlement_instructions"); e.HasKey(x => x.Id);
            e.Property(x => x.Direction).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Quantity).HasPrecision(28, 10);
            e.Property(x => x.CashAmount).HasPrecision(28, 8);
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.Property(x => x.ExternalReference).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.ExternalReference).IsUnique();
            e.HasIndex(x => new { x.Status, x.SettlementDate });
            e.HasOne(x => x.Batch).WithMany(x => x.Instructions).HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<SettlementFailure>(e =>
        {
            e.ToTable("settlement_failures"); e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.ReasonCode).HasMaxLength(64).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1024).IsRequired();
            e.HasIndex(x => new { x.InstructionId, x.DetectedAt });
            e.HasOne(x => x.Instruction).WithMany(x => x.Failures).HasForeignKey(x => x.InstructionId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

public static class SettlementModule
{
    public static IServiceCollection AddSettlementModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<SettlementDb>(o => o.UseNpgsql(connectionString, p => p.MigrationsHistoryTable("__EFMigrationsHistory", SettlementDb.Schema)));
        services.AddScoped<SettlementService>();
        services.AddSingleton<IJsonRpcMethodProvider, SettlementJsonRpcMethods>();
        return services;
    }
}
