using Microsoft.EntityFrameworkCore;

namespace Brokerage.ReferenceData;

public sealed class ReferenceDataService(ReferenceDataDb db)
{
    public async Task<Currency> SetCurrencyAsync(
        string code, string name, int minorUnits, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalizedCode = code.Trim().ToUpperInvariant();
        if (normalizedCode.Length != 3)
            throw new ArgumentException("Currency code must have three letters.", nameof(code));
        if (minorUnits is < 0 or > 4)
            throw new ArgumentOutOfRangeException(nameof(minorUnits));
        var currency = await db.Currencies.SingleOrDefaultAsync(x => x.Code == normalizedCode, cancellationToken);
        if (currency is null)
        {
            currency = new Currency { Code = normalizedCode, Name = name.Trim(), MinorUnits = minorUnits };
            db.Currencies.Add(currency);
        }
        else
        {
            currency.Name = name.Trim();
            currency.MinorUnits = minorUnits;
            currency.IsActive = true;
        }
        await db.SaveChangesAsync(cancellationToken);
        return currency;
    }

    public async Task<Country> SetCountryAsync(
        string code, string name, string currencyCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);
        var normalizedCode = code.Trim().ToUpperInvariant();
        var normalizedCurrency = currencyCode.Trim().ToUpperInvariant();
        if (normalizedCode.Length != 2)
            throw new ArgumentException("Country code must have two letters.", nameof(code));
        if (!await db.Currencies.AnyAsync(x => x.Code == normalizedCurrency && x.IsActive, cancellationToken))
            throw new KeyNotFoundException("Active currency was not found.");
        var country = await db.Countries.SingleOrDefaultAsync(x => x.Code == normalizedCode, cancellationToken);
        if (country is null)
        {
            country = new Country { Code = normalizedCode, Name = name.Trim(), CurrencyCode = normalizedCurrency };
            db.Countries.Add(country);
        }
        else
        {
            country.Name = name.Trim();
            country.CurrencyCode = normalizedCurrency;
            country.IsActive = true;
        }
        await db.SaveChangesAsync(cancellationToken);
        return country;
    }

    public async Task SetBusinessDayAsync(
        Guid calendarId, DateOnly date, bool isBusinessDay, string? description,
        CancellationToken cancellationToken = default)
    {
        if (!await db.BusinessCalendars.AnyAsync(x => x.Id == calendarId, cancellationToken))
            throw new KeyNotFoundException("Business calendar was not found.");
        var day = await db.BusinessCalendarDays.SingleOrDefaultAsync(
            x => x.CalendarId == calendarId && x.Date == date, cancellationToken);
        if (day is null)
        {
            day = new BusinessCalendarDay
            {
                Id = Guid.NewGuid(),
                CalendarId = calendarId,
                Date = date,
                IsBusinessDay = isBusinessDay,
                Description = description
            };
            db.BusinessCalendarDays.Add(day);
        }
        else
        {
            day.IsBusinessDay = isBusinessDay;
            day.Description = description;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsBusinessDayAsync(
        Guid calendarId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var overrideValue = await db.BusinessCalendarDays.AsNoTracking()
            .Where(x => x.CalendarId == calendarId && x.Date == date)
            .Select(x => (bool?)x.IsBusinessDay).SingleOrDefaultAsync(cancellationToken);
        if (!await db.BusinessCalendars.AnyAsync(x => x.Id == calendarId, cancellationToken))
            throw new KeyNotFoundException("Business calendar was not found.");
        return overrideValue ?? date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
    }
}
