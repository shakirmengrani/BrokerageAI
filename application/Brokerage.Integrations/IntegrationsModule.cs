using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Brokerage.Common;

namespace Brokerage.Integrations;

public enum IntegrationMessageStatus { Pending, Processing, Succeeded, Failed, DeadLettered }
public enum IntegrationMessageDirection { Inbound, Outbound }

public sealed class IntegrationEndpoint
{
    public Guid Id { get; init; }
    public required string Name { get; set; }
    public required string Provider { get; set; }
    public required string BaseAddress { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public IntegrationCredentialReference? CredentialReference { get; set; }
    public ICollection<IntegrationMessage> Messages { get; } = [];
}

public sealed class IntegrationCredentialReference
{
    public Guid Id { get; init; }
    public Guid EndpointId { get; init; }
    public required string SecretReference { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RotatedAt { get; set; }
}

public sealed class IntegrationMessage
{
    public Guid Id { get; init; }
    public Guid EndpointId { get; init; }
    public required string MessageReference { get; set; }
    public required string MessageType { get; set; }
    public IntegrationMessageDirection Direction { get; set; }
    public IntegrationMessageStatus Status { get; set; } = IntegrationMessageStatus.Pending;
    public string? PayloadJson { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public IntegrationEndpoint Endpoint { get; init; } = null!;
}

public sealed class IntegrationsDb(DbContextOptions<IntegrationsDb> options) : DbContext(options)
{
    public const string Schema = "integrations";
    public DbSet<IntegrationEndpoint> Endpoints => Set<IntegrationEndpoint>();
    public DbSet<IntegrationCredentialReference> CredentialReferences => Set<IntegrationCredentialReference>();
    public DbSet<IntegrationMessage> Messages => Set<IntegrationMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<IntegrationEndpoint>(e =>
        {
            e.ToTable("integration_endpoints"); e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.Property(x => x.Provider).HasMaxLength(64).IsRequired();
            e.Property(x => x.BaseAddress).HasMaxLength(512).IsRequired();
            e.HasIndex(x => new { x.Provider, x.Name }).IsUnique();
        });
        modelBuilder.Entity<IntegrationCredentialReference>(e =>
        {
            e.ToTable("integration_credential_references"); e.HasKey(x => x.Id);
            e.Property(x => x.SecretReference).HasMaxLength(512).IsRequired();
            e.HasIndex(x => x.EndpointId).IsUnique();
            e.HasOne<IntegrationEndpoint>().WithOne(x => x.CredentialReference)
                .HasForeignKey<IntegrationCredentialReference>(x => x.EndpointId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<IntegrationMessage>(e =>
        {
            e.ToTable("integration_messages"); e.HasKey(x => x.Id);
            e.Property(x => x.MessageReference).HasMaxLength(128).IsRequired();
            e.Property(x => x.MessageType).HasMaxLength(64).IsRequired();
            e.Property(x => x.Direction).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.PayloadJson).HasColumnType("jsonb");
            e.HasIndex(x => new { x.EndpointId, x.MessageReference }).IsUnique();
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasOne(x => x.Endpoint).WithMany(x => x.Messages).HasForeignKey(x => x.EndpointId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

public static class IntegrationsModule
{
    public static IServiceCollection AddIntegrationsModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<IntegrationsDb>(o => o.UseNpgsql(connectionString, p => p.MigrationsHistoryTable("__EFMigrationsHistory", IntegrationsDb.Schema)));
        services.AddScoped<IntegrationsService>();
        services.AddSingleton<IJsonRpcMethodProvider, IntegrationsJsonRpcMethods>();
        return services;
    }
}
