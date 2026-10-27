using Microsoft.EntityFrameworkCore;

namespace Brokerage.Tax;

public sealed class TaxService(TaxDb db)
{
    public async Task<TaxLot> RecordTaxLotAsync(
        Guid accountId, Guid instrumentId, string acquisitionReference, DateOnly acquiredOn,
        decimal quantity, decimal costBasis, string currency, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acquisitionReference);
        ValidateCurrency(currency);
        if (quantity <= 0 || costBasis < 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive and cost basis non-negative.");
        if (await db.TaxLots.AnyAsync(x => x.AccountId == accountId && x.AcquisitionReference == acquisitionReference, cancellationToken))
            throw new InvalidOperationException("Tax lot acquisition reference already exists.");
        var lot = new TaxLot
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            InstrumentId = instrumentId,
            AcquisitionReference = acquisitionReference.Trim(),
            AcquiredOn = acquiredOn,
            Quantity = quantity,
            CostBasis = costBasis,
            Currency = currency.ToUpperInvariant()
        };
        db.TaxLots.Add(lot);
        await db.SaveChangesAsync(cancellationToken);
        return lot;
    }

    public async Task<Withholding> RecordWithholdingAsync(
        Guid accountId, Guid? instrumentId, string paymentReference, decimal grossAmount,
        decimal taxAmount, string currency, DateOnly taxDate, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentReference);
        ValidateCurrency(currency);
        if (grossAmount < 0 || taxAmount < 0 || taxAmount > grossAmount)
            throw new ArgumentOutOfRangeException(nameof(taxAmount), "Amounts must be non-negative and tax cannot exceed gross.");
        if (await db.Withholdings.AnyAsync(x => x.AccountId == accountId && x.PaymentReference == paymentReference, cancellationToken))
            throw new InvalidOperationException("Withholding payment reference already exists.");
        var withholding = new Withholding
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            InstrumentId = instrumentId,
            PaymentReference = paymentReference.Trim(),
            GrossAmount = grossAmount,
            TaxAmount = taxAmount,
            Rate = grossAmount == 0 ? 0 : taxAmount / grossAmount,
            Currency = currency.ToUpperInvariant(),
            TaxDate = taxDate
        };
        db.Withholdings.Add(withholding);
        await db.SaveChangesAsync(cancellationToken);
        return withholding;
    }

    public async Task<TaxStatement> CreateStatementAsync(
        Guid accountId, int taxYear, string jurisdiction, string storageKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jurisdiction);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        if (taxYear is < 1900 or > 9999)
            throw new ArgumentOutOfRangeException(nameof(taxYear));
        var statement = new TaxStatement
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            TaxYear = taxYear,
            Jurisdiction = jurisdiction.Trim().ToUpperInvariant(),
            StorageKey = storageKey.Trim()
        };
        db.TaxStatements.Add(statement);
        await db.SaveChangesAsync(cancellationToken);
        return statement;
    }

    public async Task IssueStatementAsync(Guid statementId, CancellationToken cancellationToken = default)
    {
        var statement = await db.TaxStatements.SingleOrDefaultAsync(x => x.Id == statementId, cancellationToken)
            ?? throw new KeyNotFoundException("Tax statement was not found.");
        if (statement.Status != TaxStatementStatus.Draft)
            throw new InvalidOperationException("Only draft tax statements can be issued.");
        statement.Status = TaxStatementStatus.Issued;
        statement.IssuedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateCurrency(string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        if (currency.Trim().Length != 3)
            throw new ArgumentException("Currency must be a three-letter code.", nameof(currency));
    }
}
