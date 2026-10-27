using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Brokerage.Common;

namespace Brokerage.Payments;

public enum PaymentDirection { Deposit, Withdrawal }
public enum PaymentStatus { Pending, Submitted, Processing, Completed, Failed, Cancelled, Reversed }
public enum PaymentEventType { Created, Submitted, StatusChanged, Reversed }

public sealed class Payment
{
    public Guid Id { get; init; }
    public Guid AccountId { get; init; }
    public required string PaymentReference { get; set; }
    public required string IdempotencyKey { get; set; }
    public PaymentDirection Direction { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public string? ProviderReference { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public ICollection<PaymentStatusEvent> Events { get; } = [];
}

public sealed class PaymentStatusEvent
{
    public Guid Id { get; init; }
    public Guid PaymentId { get; init; }
    public PaymentEventType Type { get; set; }
    public PaymentStatus Status { get; set; }
    public string? ProviderCode { get; set; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public Payment Payment { get; init; } = null!;
}

public sealed class PaymentMethod
{
    public Guid Id { get; init; }
    public Guid ClientId { get; init; }
    public required string Provider { get; set; }
    public required string ExternalMethodReference { get; set; }
    public required string MethodType { get; set; }
    public string? DisplayLabel { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class PaymentsDb(DbContextOptions<PaymentsDb> options) : DbContext(options)
{
    public const string Schema = "payments";
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentStatusEvent> PaymentStatusEvents => Set<PaymentStatusEvent>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Payment>(e =>
        {
            e.ToTable("payments"); e.HasKey(x => x.Id);
            e.Property(x => x.PaymentReference).HasMaxLength(128).IsRequired();
            e.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
            e.Property(x => x.Direction).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Amount).HasPrecision(28, 8);
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.Property(x => x.ProviderReference).HasMaxLength(128);
            e.HasIndex(x => x.PaymentReference).IsUnique();
            e.HasIndex(x => x.IdempotencyKey).IsUnique();
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });
        modelBuilder.Entity<PaymentStatusEvent>(e =>
        {
            e.ToTable("payment_status_events"); e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.ProviderCode).HasMaxLength(64);
            e.HasIndex(x => new { x.PaymentId, x.OccurredAt });
            e.HasOne(x => x.Payment).WithMany(x => x.Events).HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PaymentMethod>(e =>
        {
            e.ToTable("payment_methods"); e.HasKey(x => x.Id);
            e.Property(x => x.Provider).HasMaxLength(64).IsRequired();
            e.Property(x => x.ExternalMethodReference).HasMaxLength(256).IsRequired();
            e.Property(x => x.MethodType).HasMaxLength(32).IsRequired();
            e.Property(x => x.DisplayLabel).HasMaxLength(128);
            e.HasIndex(x => new { x.ClientId, x.Provider, x.ExternalMethodReference }).IsUnique();
        });
    }
}

public static class PaymentsModule
{
    public static IServiceCollection AddPaymentsModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<PaymentsDb>(o => o.UseNpgsql(connectionString, p => p.MigrationsHistoryTable("__EFMigrationsHistory", PaymentsDb.Schema)));
        services.AddScoped<PaymentsService>();
        services.AddSingleton<IJsonRpcMethodProvider, PaymentsJsonRpcMethods>();
        return services;
    }
}
