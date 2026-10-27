using Microsoft.EntityFrameworkCore;

namespace Brokerage.Portfolios;

public sealed class PortfoliosService(PortfoliosDb db)
{
    public async Task<Portfolio> CreatePortfolioAsync(
        Guid clientId, string name, string baseCurrency, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ValidateCurrency(baseCurrency);
        var portfolio = new Portfolio
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = name.Trim(),
            BaseCurrency = baseCurrency.Trim().ToUpperInvariant()
        };
        db.Portfolios.Add(portfolio);
        await db.SaveChangesAsync(cancellationToken);
        return portfolio;
    }

    public async Task<Position> RecordAcquisitionAsync(
        Guid portfolioId, Guid instrumentId, decimal quantity, decimal unitCost,
        string currency, DateOnly acquiredOn, string sourceReference,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0 || unitCost < 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Acquisition quantity must be positive and unit cost non-negative.");
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceReference);
        ValidateCurrency(currency);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await db.Portfolios.AnyAsync(x => x.Id == portfolioId, cancellationToken))
            throw new KeyNotFoundException("Portfolio was not found.");
        var position = await db.Positions.SingleOrDefaultAsync(
            x => x.PortfolioId == portfolioId && x.InstrumentId == instrumentId, cancellationToken);
        if (position is null)
        {
            position = new Position
            {
                Id = Guid.NewGuid(),
                PortfolioId = portfolioId,
                InstrumentId = instrumentId,
                Quantity = 0,
                AverageCost = 0,
                Currency = currency.ToUpperInvariant()
            };
            db.Positions.Add(position);
        }
        if (!string.Equals(position.Currency, currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Position currency cannot change between acquisitions.");
        if (await db.PositionLots.AnyAsync(x => x.PositionId == position.Id && x.SourceReference == sourceReference, cancellationToken))
            throw new InvalidOperationException("The acquisition source reference has already been recorded.");

        var previousCost = position.Quantity * position.AverageCost;
        var newQuantity = position.Quantity + quantity;
        position.AverageCost = newQuantity == 0 ? 0 : (previousCost + quantity * unitCost) / newQuantity;
        position.Quantity = newQuantity;
        db.PositionLots.Add(new PositionLot
        {
            Id = Guid.NewGuid(),
            PositionId = position.Id,
            OpenQuantity = quantity,
            UnitCost = unitCost,
            AcquiredOn = acquiredOn,
            SourceReference = sourceReference.Trim()
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return position;
    }

    public async Task<PortfolioValuation> RecordValuationAsync(
        Guid portfolioId, decimal totalValue, decimal cashValue, string currency,
        DateTimeOffset valuedAt, CancellationToken cancellationToken = default)
    {
        if (totalValue < 0 || cashValue < 0 || cashValue > totalValue)
            throw new ArgumentOutOfRangeException(nameof(totalValue), "Valuation must be non-negative and cash cannot exceed total value.");
        ValidateCurrency(currency);
        if (!await db.Portfolios.AnyAsync(x => x.Id == portfolioId, cancellationToken))
            throw new KeyNotFoundException("Portfolio was not found.");
        var valuation = new PortfolioValuation
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolioId,
            TotalValue = totalValue,
            CashValue = cashValue,
            Currency = currency.ToUpperInvariant(),
            ValuedAt = valuedAt
        };
        db.Valuations.Add(valuation);
        await db.SaveChangesAsync(cancellationToken);
        return valuation;
    }

    public Task<List<Position>> GetPositionsAsync(Guid portfolioId, CancellationToken cancellationToken = default) =>
        db.Positions.AsNoTracking().Where(x => x.PortfolioId == portfolioId).OrderBy(x => x.InstrumentId).ToListAsync(cancellationToken);

    public async Task<List<Position>> GetPositionsForClientAsync(
        Guid clientId, Guid portfolioId, CancellationToken cancellationToken = default)
    {
        if (!await db.Portfolios.AnyAsync(
                x => x.Id == portfolioId && x.ClientId == clientId, cancellationToken))
            throw new KeyNotFoundException("Portfolio was not found for this client.");
        return await GetPositionsAsync(portfolioId, cancellationToken);
    }

    private static void ValidateCurrency(string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        if (currency.Trim().Length != 3)
            throw new ArgumentException("Currency must be a three-letter code.", nameof(currency));
    }
}
