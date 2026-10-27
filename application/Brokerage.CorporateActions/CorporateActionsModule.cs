using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Brokerage.Common;

namespace Brokerage.CorporateActions;

public enum CorporateActionType { CashDividend, StockSplit, RightsIssue, Merger, SpinOff, Other }
public enum ElectionStatus { NotRequired, Pending, Submitted, Accepted, Rejected, Expired }

public sealed class CorporateAction
{
    public Guid Id { get; init; }
    public Guid InstrumentId { get; init; }
    public required string ExternalReference { get; set; }
    public CorporateActionType Type { get; set; }
    public DateOnly ExDate { get; set; }
    public DateOnly? RecordDate { get; set; }
    public DateOnly? PayableDate { get; set; }
    public decimal? CashAmount { get; set; }
    public decimal? Ratio { get; set; }
    public required string Currency { get; set; }
    public bool IsProcessed { get; set; }
    public ICollection<CorporateActionElection> Elections { get; } = [];
}

public sealed class CorporateActionElection
{
    public Guid Id { get; init; }
    public Guid CorporateActionId { get; init; }
    public Guid AccountId { get; init; }
    public required string ElectionOption { get; set; }
    public decimal? Quantity { get; set; }
    public ElectionStatus Status { get; set; } = ElectionStatus.Pending;
    public DateTimeOffset? SubmittedAt { get; set; }
    public CorporateAction CorporateAction { get; init; } = null!;
}

public sealed class CorporateActionDb(DbContextOptions<CorporateActionDb> options) : DbContext(options)
{
    public const string Schema = "corporate_actions";
    public DbSet<CorporateAction> CorporateActions => Set<CorporateAction>();
    public DbSet<CorporateActionElection> Elections => Set<CorporateActionElection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<CorporateAction>(e =>
        {
            e.ToTable("corporate_actions"); e.HasKey(x => x.Id);
            e.Property(x => x.ExternalReference).HasMaxLength(128).IsRequired();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.CashAmount).HasPrecision(28, 8);
            e.Property(x => x.Ratio).HasPrecision(28, 10);
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.HasIndex(x => new { x.InstrumentId, x.ExternalReference }).IsUnique();
            e.HasIndex(x => new { x.ExDate, x.IsProcessed });
        });
        modelBuilder.Entity<CorporateActionElection>(e =>
        {
            e.ToTable("corporate_action_elections"); e.HasKey(x => x.Id);
            e.Property(x => x.ElectionOption).HasMaxLength(64).IsRequired();
            e.Property(x => x.Quantity).HasPrecision(28, 10);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasIndex(x => new { x.CorporateActionId, x.AccountId }).IsUnique();
            e.HasOne(x => x.CorporateAction).WithMany(x => x.Elections).HasForeignKey(x => x.CorporateActionId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

public static class CorporateActionsModule
{
    public static IServiceCollection AddCorporateActionsModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<CorporateActionDb>(o => o.UseNpgsql(connectionString, p => p.MigrationsHistoryTable("__EFMigrationsHistory", CorporateActionDb.Schema)));
        services.AddScoped<CorporateActionsService>();
        services.AddSingleton<IJsonRpcMethodProvider, CorporateActionsJsonRpcMethods>();
        return services;
    }
}
