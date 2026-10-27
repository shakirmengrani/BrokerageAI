using Microsoft.EntityFrameworkCore;

namespace Brokerage.Orders;

public sealed class OrdersDb : DbContext
{
    public const string Schema = "orders";

    public OrdersDb(DbContextOptions<OrdersDb> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Execution> Executions => Set<Execution>();
    public DbSet<OrderEvent> OrderEvents => Set<OrderEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ClientOrderReference).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Side).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.TimeInForce).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Quantity).HasPrecision(28, 10);
            entity.Property(x => x.LimitPrice).HasPrecision(28, 10);
            entity.HasIndex(x => new { x.AccountId, x.ClientOrderReference }).IsUnique();
            entity.HasIndex(x => new { x.Status, x.CreatedAt });
        });

        modelBuilder.Entity<Execution>(entity =>
        {
            entity.ToTable("executions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExecutionReference).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(28, 10);
            entity.Property(x => x.Price).HasPrecision(28, 10);
            entity.HasIndex(x => x.ExecutionReference).IsUnique();
            entity.HasOne(x => x.Order).WithMany(x => x.Executions)
                .HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderEvent>(entity =>
        {
            entity.ToTable("order_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(x => new { x.OrderId, x.OccurredAt });
            entity.HasOne(x => x.Order).WithMany(x => x.Events)
                .HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
