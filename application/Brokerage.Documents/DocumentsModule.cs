using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Brokerage.Common;

namespace Brokerage.Documents;

public enum DocumentStatus { Pending, Available, Quarantined, Deleted }

public sealed class StoredDocument
{
    public Guid Id { get; init; }
    public Guid? ClientId { get; set; }
    public required string DocumentType { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
    public required string CurrentStorageKey { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public ICollection<DocumentVersion> Versions { get; } = [];
}

public sealed class DocumentVersion
{
    public Guid Id { get; init; }
    public Guid DocumentId { get; init; }
    public int VersionNumber { get; set; }
    public required string StorageKey { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public DateTimeOffset UploadedAt { get; init; } = DateTimeOffset.UtcNow;
    public StoredDocument Document { get; init; } = null!;
}

public sealed class RetentionPolicy
{
    public Guid Id { get; init; }
    public required string DocumentType { get; set; }
    public int RetentionDays { get; set; }
    public bool LegalHoldByDefault { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class DocumentsDb(DbContextOptions<DocumentsDb> options) : DbContext(options)
{
    public const string Schema = "documents";
    public DbSet<StoredDocument> Documents => Set<StoredDocument>();
    public DbSet<DocumentVersion> Versions => Set<DocumentVersion>();
    public DbSet<RetentionPolicy> RetentionPolicies => Set<RetentionPolicy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<StoredDocument>(e =>
        {
            e.ToTable("documents"); e.HasKey(x => x.Id);
            e.Property(x => x.DocumentType).HasMaxLength(64).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.CurrentStorageKey).HasMaxLength(512).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.HasIndex(x => x.CurrentStorageKey).IsUnique();
            e.HasIndex(x => new { x.ClientId, x.DocumentType, x.CreatedAt });
        });
        modelBuilder.Entity<DocumentVersion>(e =>
        {
            e.ToTable("document_versions"); e.HasKey(x => x.Id);
            e.Property(x => x.StorageKey).HasMaxLength(512).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.HasIndex(x => new { x.DocumentId, x.VersionNumber }).IsUnique();
            e.HasIndex(x => x.StorageKey).IsUnique();
            e.HasOne(x => x.Document).WithMany(x => x.Versions).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<RetentionPolicy>(e =>
        {
            e.ToTable("retention_policies"); e.HasKey(x => x.Id);
            e.Property(x => x.DocumentType).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.DocumentType).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("ck_retention_policy_days", "\"RetentionDays\" > 0"));
        });
    }
}

public static class DocumentsModule
{
    public static IServiceCollection AddDocumentsModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<DocumentsDb>(o => o.UseNpgsql(connectionString, p => p.MigrationsHistoryTable("__EFMigrationsHistory", DocumentsDb.Schema)));
        services.AddScoped<DocumentsService>();
        services.AddSingleton<IJsonRpcMethodProvider, DocumentsJsonRpcMethods>();
        return services;
    }
}
