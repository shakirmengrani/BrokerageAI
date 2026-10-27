using Microsoft.EntityFrameworkCore;

namespace Brokerage.CorporateActions;

public sealed class CorporateActionsService(CorporateActionDb db)
{
    public async Task<CorporateAction> RegisterAsync(
        Guid instrumentId, string externalReference, CorporateActionType type,
        DateOnly exDate, DateOnly? recordDate, DateOnly? payableDate,
        decimal? cashAmount, decimal? ratio, string currency,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalReference);
        ValidateCurrency(currency);
        if (cashAmount < 0 || ratio <= 0)
            throw new ArgumentOutOfRangeException(nameof(cashAmount), "Cash amount must be non-negative and ratio positive.");
        if (recordDate < exDate || payableDate < exDate)
            throw new ArgumentException("Record and payable dates cannot precede the ex-date.");
        if (await db.CorporateActions.AnyAsync(x => x.InstrumentId == instrumentId && x.ExternalReference == externalReference, cancellationToken))
            throw new InvalidOperationException("Corporate action reference already exists for this instrument.");

        var item = new CorporateAction
        {
            Id = Guid.NewGuid(),
            InstrumentId = instrumentId,
            ExternalReference = externalReference.Trim(),
            Type = type,
            ExDate = exDate,
            RecordDate = recordDate,
            PayableDate = payableDate,
            CashAmount = cashAmount,
            Ratio = ratio,
            Currency = currency.ToUpperInvariant()
        };
        db.CorporateActions.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task<CorporateActionElection> RecordElectionAsync(
        Guid actionId, Guid accountId, string electionOption, decimal? quantity,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(electionOption);
        if (quantity is <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        var action = await db.CorporateActions.SingleOrDefaultAsync(x => x.Id == actionId, cancellationToken)
            ?? throw new KeyNotFoundException("Corporate action was not found.");
        if (action.IsProcessed || action.RecordDate is not null && DateOnly.FromDateTime(DateTime.UtcNow) > action.RecordDate)
            throw new InvalidOperationException("Elections are closed for this corporate action.");
        if (await db.Elections.AnyAsync(x => x.CorporateActionId == actionId && x.AccountId == accountId, cancellationToken))
            throw new InvalidOperationException("An election for this account already exists.");
        var election = new CorporateActionElection
        {
            Id = Guid.NewGuid(),
            CorporateActionId = actionId,
            AccountId = accountId,
            ElectionOption = electionOption.Trim(),
            Quantity = quantity,
            Status = ElectionStatus.Submitted,
            SubmittedAt = DateTimeOffset.UtcNow
        };
        db.Elections.Add(election);
        await db.SaveChangesAsync(cancellationToken);
        return election;
    }

    public async Task MarkProcessedAsync(Guid actionId, CancellationToken cancellationToken = default)
    {
        var action = await db.CorporateActions.SingleOrDefaultAsync(x => x.Id == actionId, cancellationToken)
            ?? throw new KeyNotFoundException("Corporate action was not found.");
        if (action.IsProcessed)
            throw new InvalidOperationException("Corporate action is already processed.");
        action.IsProcessed = true;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateCurrency(string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        if (currency.Trim().Length != 3)
            throw new ArgumentException("Currency must be a three-letter code.", nameof(currency));
    }
}
