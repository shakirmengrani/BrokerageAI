using Microsoft.EntityFrameworkCore;

namespace Brokerage.Clients;

public sealed class ClientsDb : DbContext
{
    public const string Schema = "clients";

    public ClientsDb(DbContextOptions<ClientsDb> options) : base(options) { }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientDocument> ClientDocuments => Set<ClientDocument>();
    public DbSet<ClientContact> ClientContacts => Set<ClientContact>();
    public DbSet<KycRecord> KycRecords => Set<KycRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Client>(entity =>
        {
            entity.ToTable("clients");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalReference).HasMaxLength(64).IsRequired();
            entity.Property(x => x.GivenName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.FamilyName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => x.ExternalReference).IsUnique();
            entity.HasMany(x => x.Contacts).WithOne(x => x.Client)
                .HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Documents).WithOne(x => x.Client)
                .HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.KycRecords).WithOne(x => x.Client)
                .HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ClientContact>(entity =>
        {
            entity.ToTable("client_contacts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Value).HasMaxLength(512).IsRequired();
        });

        modelBuilder.Entity<ClientDocument>(entity =>
        {
            entity.ToTable("client_documents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DocumentType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.StorageKey).HasMaxLength(512).IsRequired();
        });

        modelBuilder.Entity<KycRecord>(entity =>
        {
            entity.ToTable("kyc_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ReviewedBy).HasMaxLength(128);
            entity.HasIndex(x => new { x.ClientId, x.CreatedAt });
        });
    }
}
