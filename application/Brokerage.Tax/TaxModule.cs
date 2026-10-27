using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Brokerage.Common;

namespace Brokerage.Tax;

public enum TaxStatementStatus { Draft, Finalized, Issued, Corrected }

public sealed class TaxLot
{
    public Guid Id { get; init; }
    public Guid AccountId { get; init; }
    public Guid InstrumentId { get; init; }
    public required string AcquisitionReference { get; set; }
    public DateOnly AcquiredOn { get; set; }
    public decimal Quantity { get; set; }
    public decimal CostBasis { get; set; }
    public required string Currency { get; set; }
}

public sealed class Withholding
{
    public Guid Id { get; init; }
    public Guid AccountId { get; init; }
    public Guid? InstrumentId { get; set; }
    public required string PaymentReference { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Rate { get; set; }
    public required string Currency { get; set; }
    public DateOnly TaxDate { get; set; }
}

public sealed class TaxStatement
{
    public Guid Id { get; init; }
    public Guid AccountId { get; init; }
    public int TaxYear { get; set; }
    public required string Jurisdiction { get; set; }
    public TaxStatementStatus Status { get; set; } = TaxStatementStatus.Draft;
    public required string StorageKey { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? IssuedAt { get; set; }
}

public sealed class TaxDb(DbContextOptions<TaxDb> options) : DbContext(options)
{
    public const string Schema = "tax";
    public DbSet<TaxLot> TaxLots => Set<TaxLot>();
    public DbSet<Withholding> Withholdings => Set<Withholding>();
    public DbSet<TaxStatement> TaxStatements => Set<TaxStatement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<TaxLot>(e =>
        {
            e.ToTable("tax_lots"); e.HasKey(x => x.Id);
            e.Property(x => x.AcquisitionReference).HasMaxLength(128).IsRequired();
            e.Property(x => x.Quantity).HasPrecision(28, 10);
            e.Property(x => x.CostBasis).HasPrecision(28, 8);
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.HasIndex(x => new { x.AccountId, x.AcquisitionReference }).IsUnique();
            e.HasIndex(x => new { x.AccountId, x.InstrumentId, x.AcquiredOn });
        });
        modelBuilder.Entity<Withholding>(e =>
        {
            e.ToTable("withholdings"); e.HasKey(x => x.Id);
            e.Property(x => x.PaymentReference).HasMaxLength(128).IsRequired();
            e.Property(x => x.GrossAmount).HasPrecision(28, 8);
            e.Property(x => x.TaxAmount).HasPrecision(28, 8);
            e.Property(x => x.Rate).HasPrecision(9, 6);
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.HasIndex(x => new { x.AccountId, x.PaymentReference }).IsUnique();
            e.HasIndex(x => new { x.AccountId, x.TaxDate });
        });
        modelBuilder.Entity<TaxStatement>(e =>
        {
            e.ToTable("tax_statements"); e.HasKey(x => x.Id);
            e.Property(x => x.Jurisdiction).HasMaxLength(16).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.StorageKey).HasMaxLength(512).IsRequired();
            e.HasIndex(x => new { x.AccountId, x.TaxYear, x.Jurisdiction }).IsUnique();
        });
    }
}

public static class TaxModule
{
    public static IServiceCollection AddTaxModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<TaxDb>(o => o.UseNpgsql(connectionString, p => p.MigrationsHistoryTable("__EFMigrationsHistory", TaxDb.Schema)));
        services.AddScoped<TaxService>();
        services.AddSingleton<IJsonRpcMethodProvider, TaxJsonRpcMethods>();
        return services;
    }
}
