using Microsoft.EntityFrameworkCore;

namespace Brokerage.Billing;

public sealed class BillingService(BillingDb db)
{
    public async Task<FeeSchedule> CreateFeeScheduleAsync(
        string name, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveUntil,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (effectiveUntil is not null && effectiveUntil <= effectiveFrom)
            throw new ArgumentException("Effective end must be later than effective start.", nameof(effectiveUntil));
        var schedule = new FeeSchedule
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            EffectiveFrom = effectiveFrom,
            EffectiveUntil = effectiveUntil
        };
        db.FeeSchedules.Add(schedule);
        await db.SaveChangesAsync(cancellationToken);
        return schedule;
    }

    public async Task<CommissionRule> AddCommissionRuleAsync(
        Guid feeScheduleId, string instrumentType, decimal rate, decimal minimumAmount,
        decimal? maximumAmount, string currency, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instrumentType);
        ValidateCurrency(currency);
        if (rate < 0 || minimumAmount < 0 || maximumAmount < minimumAmount)
            throw new ArgumentOutOfRangeException(nameof(rate), "Commission terms must be non-negative and maximum at least minimum.");
        if (!await db.FeeSchedules.AnyAsync(x => x.Id == feeScheduleId && x.IsActive, cancellationToken))
            throw new KeyNotFoundException("Active fee schedule was not found.");
        var rule = new CommissionRule
        {
            Id = Guid.NewGuid(),
            FeeScheduleId = feeScheduleId,
            InstrumentType = instrumentType.Trim(),
            Rate = rate,
            MinimumAmount = minimumAmount,
            MaximumAmount = maximumAmount,
            Currency = currency.ToUpperInvariant()
        };
        db.CommissionRules.Add(rule);
        await db.SaveChangesAsync(cancellationToken);
        return rule;
    }

    public async Task<decimal> CalculateCommissionAsync(
        Guid feeScheduleId, string instrumentType, decimal notional, CancellationToken cancellationToken = default)
    {
        if (notional < 0)
            throw new ArgumentOutOfRangeException(nameof(notional));
        var now = DateTimeOffset.UtcNow;
        var rule = await db.CommissionRules.AsNoTracking()
            .Where(x => x.FeeScheduleId == feeScheduleId && x.InstrumentType == instrumentType
                && x.FeeSchedule.IsActive && x.FeeSchedule.EffectiveFrom <= now
                && (x.FeeSchedule.EffectiveUntil == null || x.FeeSchedule.EffectiveUntil > now))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("No active commission rule was found.");
        var amount = Math.Max(rule.MinimumAmount, notional * rule.Rate);
        return rule.MaximumAmount is null ? amount : Math.Min(amount, rule.MaximumAmount.Value);
    }

    public async Task<Charge> CreateChargeAsync(
        Guid accountId, Guid? orderId, string reference, string chargeType,
        decimal amount, string currency, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(chargeType);
        ValidateCurrency(currency);
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (await db.Charges.AnyAsync(
                x => x.AccountId == accountId && x.Reference == reference, cancellationToken))
            throw new InvalidOperationException("Charge reference already exists for this account.");
        var charge = new Charge
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            OrderId = orderId,
            Reference = reference.Trim(),
            ChargeType = chargeType.Trim(),
            Amount = amount,
            Currency = currency.Trim().ToUpperInvariant()
        };
        db.Charges.Add(charge);
        await db.SaveChangesAsync(cancellationToken);
        return charge;
    }

    public async Task<Invoice> CreateInvoiceAsync(
        Guid accountId, IReadOnlyCollection<Guid> chargeIds, string invoiceNumber,
        DateOnly issuedOn, DateOnly dueOn, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceNumber);
        ArgumentNullException.ThrowIfNull(chargeIds);
        if (chargeIds.Count == 0 || chargeIds.Distinct().Count() != chargeIds.Count)
            throw new ArgumentException("Invoice requires a non-empty set of distinct charges.", nameof(chargeIds));
        if (dueOn < issuedOn)
            throw new ArgumentException("Invoice due date cannot precede its issue date.", nameof(dueOn));

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var charges = await db.Charges.Where(x => chargeIds.Contains(x.Id)).ToListAsync(cancellationToken);
        if (charges.Count != chargeIds.Count || charges.Any(x => x.AccountId != accountId || x.Status != ChargeStatus.Pending))
            throw new InvalidOperationException("All charges must exist, belong to the account, and be pending.");
        var currencies = charges.Select(x => x.Currency).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (currencies.Length != 1)
            throw new InvalidOperationException("An invoice cannot combine charges in different currencies.");
        if (await db.Invoices.AnyAsync(x => x.InvoiceNumber == invoiceNumber, cancellationToken))
            throw new InvalidOperationException("Invoice number already exists.");

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            InvoiceNumber = invoiceNumber.Trim(),
            IssuedOn = issuedOn,
            DueOn = dueOn,
            TotalAmount = charges.Sum(x => x.Amount),
            Currency = currencies[0],
            Status = InvoiceStatus.Issued
        };
        foreach (var charge in charges)
        {
            charge.Status = ChargeStatus.Invoiced;
            charge.Invoice = invoice;
        }
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return invoice;
    }

    private static void ValidateCurrency(string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        if (currency.Trim().Length != 3)
            throw new ArgumentException("Currency must be a three-letter code.", nameof(currency));
    }
}
