using Microsoft.EntityFrameworkCore;

namespace Brokerage.MarketData;

public sealed class MarketDataDb : DbContext
{
    public const string Schema = "market_data";

    public MarketDataDb(DbContextOptions<MarketDataDb> options) : base(options) { }

    public DbSet<Instrument> Instruments => Set<Instrument>();
    public DbSet<Exchange> Exchanges => Set<Exchange>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<PriceBar> PriceBars => Set<PriceBar>();
    public DbSet<CorporateAction> CorporateActions => Set<CorporateAction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Exchange>(entity =>
        {
            entity.ToTable("exchanges");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.CountryCode).HasMaxLength(2).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<Instrument>(entity =>
        {
            entity.ToTable("instruments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Symbol).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.InstrumentType).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => new { x.ExchangeId, x.Symbol }).IsUnique();
            entity.HasOne(x => x.Exchange).WithMany(x => x.Instruments)
                .HasForeignKey(x => x.ExchangeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Quote>(entity =>
        {
            entity.ToTable("quotes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Bid).HasPrecision(28, 10);
            entity.Property(x => x.Ask).HasPrecision(28, 10);
            entity.HasIndex(x => new { x.InstrumentId, x.ObservedAt });
            entity.HasOne(x => x.Instrument).WithMany(x => x.Quotes)
                .HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PriceBar>(entity =>
        {
            entity.ToTable("price_bars");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Interval).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Open).HasPrecision(28, 10);
            entity.Property(x => x.High).HasPrecision(28, 10);
            entity.Property(x => x.Low).HasPrecision(28, 10);
            entity.Property(x => x.Close).HasPrecision(28, 10);
            entity.Property(x => x.Volume).HasPrecision(28, 8);
            entity.HasIndex(x => new { x.InstrumentId, x.Interval, x.StartsAt }).IsUnique();
            entity.HasOne(x => x.Instrument).WithMany(x => x.PriceBars)
                .HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CorporateAction>(entity =>
        {
            entity.ToTable("corporate_actions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.CashAmount).HasPrecision(28, 10);
            entity.Property(x => x.Ratio).HasPrecision(28, 10);
            entity.Property(x => x.Reference).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => new { x.InstrumentId, x.Reference }).IsUnique();
            entity.HasOne(x => x.Instrument).WithMany(x => x.CorporateActions)
                .HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
