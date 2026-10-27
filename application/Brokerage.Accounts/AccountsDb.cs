using Microsoft.EntityFrameworkCore;

namespace Brokerage.Accounts;

public sealed class AccountsDb : DbContext
{
    public const string Schema = "accounts";

    public AccountsDb(DbContextOptions<AccountsDb> options) : base(options) { }

    public DbSet<TradingAccount> Accounts => Set<TradingAccount>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<TradingAccount>(entity =>
        {
            entity.ToTable("accounts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AccountNumber).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => x.AccountNumber).IsUnique();
            entity.HasIndex(x => x.ClientId);
        });

        modelBuilder.Entity<JournalEntry>(entity =>
        {
            entity.ToTable("journal_entries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Reference).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.AccountId, x.Reference }).IsUnique();
            entity.HasOne(x => x.Account).WithMany(x => x.JournalEntries)
                .HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LedgerEntry>(entity =>
        {
            entity.ToTable("ledger_entries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.Amount).HasPrecision(28, 8);
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.HasOne(x => x.JournalEntry).WithMany(x => x.LedgerEntries)
                .HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CashMovement>(entity =>
        {
            entity.ToTable("cash_movements");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalReference).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Direction).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Amount).HasPrecision(28, 8);
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.HasIndex(x => new { x.AccountId, x.ExternalReference }).IsUnique();
            entity.HasOne(x => x.Account).WithMany(x => x.CashMovements)
                .HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
