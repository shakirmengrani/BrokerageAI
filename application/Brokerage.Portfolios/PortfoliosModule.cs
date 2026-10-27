using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Brokerage.Common;

namespace Brokerage.Portfolios;

public enum PositionLotMethod { Fifo, Lifo, AverageCost, SpecificIdentification }

public sealed class Portfolio
{
    public Guid Id { get; init; }
    public Guid ClientId { get; init; }
    public required string Name { get; set; }
    public required string BaseCurrency { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public ICollection<Position> Positions { get; } = [];
    public ICollection<PortfolioValuation> Valuations { get; } = [];
}

public sealed class Position
{
    public Guid Id { get; init; }
    public Guid PortfolioId { get; init; }
    public Guid InstrumentId { get; init; }
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
    public required string Currency { get; set; }
    public Portfolio Portfolio { get; init; } = null!;
    public ICollection<PositionLot> Lots { get; } = [];
}

public sealed class PositionLot
{
    public Guid Id { get; init; }
    public Guid PositionId { get; init; }
    public decimal OpenQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public DateOnly AcquiredOn { get; set; }
    public required string SourceReference { get; set; }
    public Position Position { get; init; } = null!;
}

public sealed class PortfolioValuation
{
    public Guid Id { get; init; }
    public Guid PortfolioId { get; init; }
    public DateTimeOffset ValuedAt { get; set; }
    public decimal TotalValue { get; set; }
    public decimal CashValue { get; set; }
    public required string Currency { get; set; }
    public Portfolio Portfolio { get; init; } = null!;
}

public sealed class PortfoliosDb(DbContextOptions<PortfoliosDb> options) : DbContext(options)
{
    public const string Schema = "portfolios";
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<PositionLot> PositionLots => Set<PositionLot>();
    public DbSet<PortfolioValuation> Valuations => Set<PortfolioValuation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Portfolio>(e =>
        {
            e.ToTable("portfolios"); e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.Property(x => x.BaseCurrency).HasMaxLength(3).IsRequired();
            e.HasIndex(x => new { x.ClientId, x.Name }).IsUnique();
        });
        modelBuilder.Entity<Position>(e =>
        {
            e.ToTable("positions"); e.HasKey(x => x.Id);
            e.Property(x => x.Quantity).HasPrecision(28, 10);
            e.Property(x => x.AverageCost).HasPrecision(28, 10);
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.HasIndex(x => new { x.PortfolioId, x.InstrumentId }).IsUnique();
            e.HasOne(x => x.Portfolio).WithMany(x => x.Positions).HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PositionLot>(e =>
        {
            e.ToTable("position_lots"); e.HasKey(x => x.Id);
            e.Property(x => x.OpenQuantity).HasPrecision(28, 10);
            e.Property(x => x.UnitCost).HasPrecision(28, 10);
            e.Property(x => x.SourceReference).HasMaxLength(128).IsRequired();
            e.HasIndex(x => new { x.PositionId, x.SourceReference }).IsUnique();
            e.HasOne(x => x.Position).WithMany(x => x.Lots).HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PortfolioValuation>(e =>
        {
            e.ToTable("valuations"); e.HasKey(x => x.Id);
            e.Property(x => x.TotalValue).HasPrecision(28, 8);
            e.Property(x => x.CashValue).HasPrecision(28, 8);
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.HasIndex(x => new { x.PortfolioId, x.ValuedAt });
            e.HasOne(x => x.Portfolio).WithMany(x => x.Valuations).HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

public static class PortfoliosModule
{
    public static IServiceCollection AddPortfoliosModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<PortfoliosDb>(o => o.UseNpgsql(connectionString, p => p.MigrationsHistoryTable("__EFMigrationsHistory", PortfoliosDb.Schema)));
        services.AddScoped<PortfoliosService>();
        services.AddSingleton<IJsonRpcMethodProvider, PortfoliosJsonRpcMethods>();
        return services;
    }
}
