using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Brokerage.Common;

namespace Brokerage.ReferenceData;

public sealed class Currency
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public int MinorUnits { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Country
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string CurrencyCode { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class BusinessCalendar
{
    public Guid Id { get; init; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string TimeZoneId { get; set; }
    public ICollection<BusinessCalendarDay> Days { get; } = [];
}

public sealed class BusinessCalendarDay
{
    public Guid Id { get; init; }
    public Guid CalendarId { get; init; }
    public DateOnly Date { get; set; }
    public bool IsBusinessDay { get; set; }
    public string? Description { get; set; }
    public BusinessCalendar Calendar { get; init; } = null!;
}

public sealed class Classification
{
    public Guid Id { get; init; }
    public required string ClassificationType { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ReferenceDataDb(DbContextOptions<ReferenceDataDb> options) : DbContext(options)
{
    public const string Schema = "reference_data";
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<BusinessCalendar> BusinessCalendars => Set<BusinessCalendar>();
    public DbSet<BusinessCalendarDay> BusinessCalendarDays => Set<BusinessCalendarDay>();
    public DbSet<Classification> Classifications => Set<Classification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Currency>(e =>
        {
            e.ToTable("currencies"); e.HasKey(x => x.Code);
            e.Property(x => x.Code).HasMaxLength(3);
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.ToTable(t => t.HasCheckConstraint("ck_currency_minor_units", "\"MinorUnits\" BETWEEN 0 AND 4"));
        });
        modelBuilder.Entity<Country>(e =>
        {
            e.ToTable("countries"); e.HasKey(x => x.Code);
            e.Property(x => x.Code).HasMaxLength(2);
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
            e.HasOne<Currency>().WithMany().HasForeignKey(x => x.CurrencyCode).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<BusinessCalendar>(e =>
        {
            e.ToTable("business_calendars"); e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(32).IsRequired();
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.Property(x => x.TimeZoneId).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
        });
        modelBuilder.Entity<BusinessCalendarDay>(e =>
        {
            e.ToTable("business_calendar_days"); e.HasKey(x => x.Id);
            e.Property(x => x.Description).HasMaxLength(256);
            e.HasIndex(x => new { x.CalendarId, x.Date }).IsUnique();
            e.HasOne(x => x.Calendar).WithMany(x => x.Days).HasForeignKey(x => x.CalendarId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Classification>(e =>
        {
            e.ToTable("classifications"); e.HasKey(x => x.Id);
            e.Property(x => x.ClassificationType).HasMaxLength(64).IsRequired();
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.HasIndex(x => new { x.ClassificationType, x.Code }).IsUnique();
        });
    }
}

public static class ReferenceDataModule
{
    public static IServiceCollection AddReferenceDataModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ReferenceDataDb>(o => o.UseNpgsql(connectionString, p => p.MigrationsHistoryTable("__EFMigrationsHistory", ReferenceDataDb.Schema)));
        services.AddScoped<ReferenceDataService>();
        services.AddSingleton<IJsonRpcMethodProvider, ReferenceDataJsonRpcMethods>();
        return services;
    }
}
