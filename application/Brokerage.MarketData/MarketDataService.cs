using Microsoft.EntityFrameworkCore;

namespace Brokerage.MarketData;

public sealed class MarketDataService(MarketDataDb db)
{
    public async Task<Instrument> CreateInstrumentAsync(
        Guid exchangeId, string symbol, string name, string currency, string instrumentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(instrumentType);
        ValidateCurrency(currency);
        if (!await db.Exchanges.AnyAsync(x => x.Id == exchangeId, cancellationToken))
            throw new KeyNotFoundException("Exchange was not found.");

        var instrument = new Instrument
        {
            Id = Guid.NewGuid(),
            ExchangeId = exchangeId,
            Symbol = symbol.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Currency = currency.Trim().ToUpperInvariant(),
            InstrumentType = instrumentType.Trim()
        };
        db.Instruments.Add(instrument);
        await db.SaveChangesAsync(cancellationToken);
        return instrument;
    }

    public async Task<Quote> RecordQuoteAsync(
        Guid instrumentId, decimal bid, decimal ask, DateTimeOffset observedAt,
        CancellationToken cancellationToken = default)
    {
        if (bid < 0 || ask < bid)
            throw new ArgumentOutOfRangeException(nameof(ask), "Quote prices must be non-negative and ask must be at least bid.");
        if (observedAt > DateTimeOffset.UtcNow)
            throw new ArgumentOutOfRangeException(nameof(observedAt), "Quote observation cannot be in the future.");
        if (!await db.Instruments.AnyAsync(x => x.Id == instrumentId && x.IsActive, cancellationToken))
            throw new KeyNotFoundException("Active instrument was not found.");

        var quote = new Quote { Id = Guid.NewGuid(), InstrumentId = instrumentId, Bid = bid, Ask = ask, ObservedAt = observedAt };
        db.Quotes.Add(quote);
        await db.SaveChangesAsync(cancellationToken);
        return quote;
    }

    public Task<Quote?> GetLatestQuoteAsync(Guid instrumentId, CancellationToken cancellationToken = default) =>
        db.Quotes.AsNoTracking().Where(x => x.InstrumentId == instrumentId)
            .OrderByDescending(x => x.ObservedAt).FirstOrDefaultAsync(cancellationToken);

    private static void ValidateCurrency(string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        if (currency.Trim().Length != 3)
            throw new ArgumentException("Currency must be a three-letter code.", nameof(currency));
    }
}
