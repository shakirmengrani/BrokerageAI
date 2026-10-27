namespace Brokerage.MarketData;

public enum CorporateActionType
{
    CashDividend,
    StockSplit,
    Merger,
    RightsIssue,
    Other
}

public sealed class Exchange
{
    public Guid Id { get; init; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string CountryCode { get; set; }
    public ICollection<Instrument> Instruments { get; } = [];
}

public sealed class Instrument
{
    public Guid Id { get; init; }
    public Guid ExchangeId { get; init; }
    public required string Symbol { get; set; }
    public required string Name { get; set; }
    public required string Currency { get; set; }
    public required string InstrumentType { get; set; }
    public bool IsActive { get; set; } = true;
    public Exchange Exchange { get; init; } = null!;
    public ICollection<Quote> Quotes { get; } = [];
    public ICollection<PriceBar> PriceBars { get; } = [];
    public ICollection<CorporateAction> CorporateActions { get; } = [];
}

public sealed class Quote
{
    public Guid Id { get; init; }
    public Guid InstrumentId { get; init; }
    public decimal Bid { get; set; }
    public decimal Ask { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public Instrument Instrument { get; init; } = null!;
}

public sealed class PriceBar
{
    public Guid Id { get; init; }
    public Guid InstrumentId { get; init; }
    public required string Interval { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
    public Instrument Instrument { get; init; } = null!;
}

public sealed class CorporateAction
{
    public Guid Id { get; init; }
    public Guid InstrumentId { get; init; }
    public CorporateActionType Type { get; set; }
    public DateOnly ExDate { get; set; }
    public decimal? CashAmount { get; set; }
    public decimal? Ratio { get; set; }
    public required string Reference { get; set; }
    public Instrument Instrument { get; init; } = null!;
}
