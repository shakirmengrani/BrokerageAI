using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Brokerage.Common;

namespace Brokerage.Billing;

public enum ChargeStatus { Pending, Invoiced, Waived, Reversed }
public enum InvoiceStatus { Draft, Issued, Paid, PartiallyPaid, Void, Overdue }

public sealed class FeeSchedule
{
    public Guid Id { get; init; }
    public required string Name { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveUntil { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<CommissionRule> CommissionRules { get; } = [];
}

public sealed class CommissionRule
{
    public Guid Id { get; init; }
    public Guid FeeScheduleId { get; init; }
    public required string InstrumentType { get; set; }
    public decimal Rate { get; set; }
    public decimal MinimumAmount { get; set; }
    public decimal? MaximumAmount { get; set; }
    public required string Currency { get; set; }
    public FeeSchedule FeeSchedule { get; init; } = null!;
}

public sealed class Charge
{
    public Guid Id { get; init; }
    public Guid AccountId { get; init; }
    public Guid? OrderId { get; set; }
    public required string Reference { get; set; }
    public required string ChargeType { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public ChargeStatus Status { get; set; } = ChargeStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public Guid? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
}

public sealed class Invoice
{
    public Guid Id { get; init; }
    public Guid AccountId { get; init; }
    public required string InvoiceNumber { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public DateOnly IssuedOn { get; set; }
    public DateOnly DueOn { get; set; }
    public decimal TotalAmount { get; set; }
    public required string Currency { get; set; }
    public ICollection<Charge> Charges { get; } = [];
}

public sealed class BillingDb(DbContextOptions<BillingDb> options) : DbContext(options)
{
    public const string Schema = "billing";
    public DbSet<FeeSchedule> FeeSchedules => Set<FeeSchedule>();
    public DbSet<CommissionRule> CommissionRules => Set<CommissionRule>();
    public DbSet<Charge> Charges => Set<Charge>();
    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<FeeSchedule>(e =>
        {
            e.ToTable("fee_schedules"); e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.HasIndex(x => new { x.Name, x.EffectiveFrom }).IsUnique();
        });
        modelBuilder.Entity<CommissionRule>(e =>
        {
            e.ToTable("commission_rules"); e.HasKey(x => x.Id);
            e.Property(x => x.InstrumentType).HasMaxLength(32).IsRequired();
            e.Property(x => x.Rate).HasPrecision(18, 10);
            e.Property(x => x.MinimumAmount).HasPrecision(28, 8);
            e.Property(x => x.MaximumAmount).HasPrecision(28, 8);
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.HasIndex(x => new { x.FeeScheduleId, x.InstrumentType }).IsUnique();
            e.HasOne(x => x.FeeSchedule).WithMany(x => x.CommissionRules).HasForeignKey(x => x.FeeScheduleId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Charge>(e =>
        {
            e.ToTable("charges"); e.HasKey(x => x.Id);
            e.Property(x => x.Reference).HasMaxLength(128).IsRequired();
            e.Property(x => x.ChargeType).HasMaxLength(64).IsRequired();
            e.Property(x => x.Amount).HasPrecision(28, 8);
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasIndex(x => new { x.AccountId, x.Reference }).IsUnique();
            e.HasOne(x => x.Invoice).WithMany(x => x.Charges).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Invoice>(e =>
        {
            e.ToTable("invoices"); e.HasKey(x => x.Id);
            e.Property(x => x.InvoiceNumber).HasMaxLength(64).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.TotalAmount).HasPrecision(28, 8);
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.HasIndex(x => x.InvoiceNumber).IsUnique();
        });
    }
}

public static class BillingModule
{
    public static IServiceCollection AddBillingModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<BillingDb>(o => o.UseNpgsql(connectionString, p => p.MigrationsHistoryTable("__EFMigrationsHistory", BillingDb.Schema)));
        services.AddScoped<BillingService>();
        services.AddSingleton<IJsonRpcMethodProvider, BillingJsonRpcMethods>();
        return services;
    }
}
