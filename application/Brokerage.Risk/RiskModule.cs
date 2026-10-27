using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Brokerage.Common;

namespace Brokerage.Risk;

public enum RiskAlertStatus { Open, Acknowledged, Closed }
public enum ComplianceCaseStatus { Open, UnderReview, Closed, Escalated }

public sealed class RiskLimit
{
    public Guid Id { get; init; }
    public Guid? AccountId { get; set; }
    public required string LimitType { get; set; }
    public decimal MaximumValue { get; set; }
    public required string Currency { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class MarginRequirement
{
    public Guid Id { get; init; }
    public Guid AccountId { get; init; }
    public Guid InstrumentId { get; init; }
    public decimal InitialRate { get; set; }
    public decimal MaintenanceRate { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveUntil { get; set; }
}

public sealed class RiskAlert
{
    public Guid Id { get; init; }
    public Guid? AccountId { get; set; }
    public required string AlertType { get; set; }
    public required string Severity { get; set; }
    public required string Description { get; set; }
    public RiskAlertStatus Status { get; set; } = RiskAlertStatus.Open;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; set; }
}

public sealed class ComplianceCase
{
    public Guid Id { get; init; }
    public Guid? ClientId { get; set; }
    public Guid? AccountId { get; set; }
    public required string CaseReference { get; set; }
    public required string CaseType { get; set; }
    public ComplianceCaseStatus Status { get; set; } = ComplianceCaseStatus.Open;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; set; }
}

public sealed class RiskDb(DbContextOptions<RiskDb> options) : DbContext(options)
{
    public const string Schema = "risk";
    public DbSet<RiskLimit> RiskLimits => Set<RiskLimit>();
    public DbSet<MarginRequirement> MarginRequirements => Set<MarginRequirement>();
    public DbSet<RiskAlert> Alerts => Set<RiskAlert>();
    public DbSet<ComplianceCase> ComplianceCases => Set<ComplianceCase>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<RiskLimit>(e =>
        {
            e.ToTable("risk_limits"); e.HasKey(x => x.Id);
            e.Property(x => x.LimitType).HasMaxLength(64).IsRequired();
            e.Property(x => x.MaximumValue).HasPrecision(28, 8);
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.HasIndex(x => new { x.AccountId, x.LimitType }).IsUnique();
        });
        modelBuilder.Entity<MarginRequirement>(e =>
        {
            e.ToTable("margin_requirements"); e.HasKey(x => x.Id);
            e.Property(x => x.InitialRate).HasPrecision(9, 6);
            e.Property(x => x.MaintenanceRate).HasPrecision(9, 6);
            e.HasIndex(x => new { x.AccountId, x.InstrumentId, x.EffectiveFrom }).IsUnique();
        });
        modelBuilder.Entity<RiskAlert>(e =>
        {
            e.ToTable("alerts"); e.HasKey(x => x.Id);
            e.Property(x => x.AlertType).HasMaxLength(64).IsRequired();
            e.Property(x => x.Severity).HasMaxLength(32).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2048).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });
        modelBuilder.Entity<ComplianceCase>(e =>
        {
            e.ToTable("compliance_cases"); e.HasKey(x => x.Id);
            e.Property(x => x.CaseReference).HasMaxLength(128).IsRequired();
            e.Property(x => x.CaseType).HasMaxLength(64).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasIndex(x => x.CaseReference).IsUnique();
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });
    }
}

public static class RiskModule
{
    public static IServiceCollection AddRiskModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<RiskDb>(o => o.UseNpgsql(connectionString, p => p.MigrationsHistoryTable("__EFMigrationsHistory", RiskDb.Schema)));
        services.AddScoped<RiskService>();
        services.AddSingleton<IJsonRpcMethodProvider, RiskJsonRpcMethods>();
        return services;
    }
}
