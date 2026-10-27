using Microsoft.EntityFrameworkCore;

namespace Brokerage.Risk;

public sealed class RiskService(RiskDb db)
{
    public async Task<RiskLimit> SetLimitAsync(
        Guid? accountId, string limitType, decimal maximumValue, string currency,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(limitType);
        ValidateCurrency(currency);
        if (maximumValue < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumValue));
        var limit = await db.RiskLimits.SingleOrDefaultAsync(
            x => x.AccountId == accountId && x.LimitType == limitType, cancellationToken);
        if (limit is null)
        {
            limit = new RiskLimit
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                LimitType = limitType.Trim(),
                MaximumValue = maximumValue,
                Currency = currency.ToUpperInvariant()
            };
            db.RiskLimits.Add(limit);
        }
        else
        {
            limit.MaximumValue = maximumValue;
            limit.Currency = currency.ToUpperInvariant();
            limit.IsActive = true;
        }
        await db.SaveChangesAsync(cancellationToken);
        return limit;
    }

    public async Task<bool> IsWithinLimitAsync(
        Guid accountId, string limitType, decimal proposedValue, string currency,
        CancellationToken cancellationToken = default)
    {
        if (proposedValue < 0)
            throw new ArgumentOutOfRangeException(nameof(proposedValue));
        var limit = await db.RiskLimits.AsNoTracking().SingleOrDefaultAsync(
            x => x.IsActive && x.AccountId == accountId
                && x.LimitType == limitType && x.Currency == currency,
            cancellationToken);
        limit ??= await db.RiskLimits.AsNoTracking().SingleOrDefaultAsync(
            x => x.IsActive && x.AccountId == null && x.LimitType == limitType && x.Currency == currency,
            cancellationToken);
        return limit is null || proposedValue <= limit.MaximumValue;
    }

    public async Task<RiskAlert> CreateAlertAsync(
        Guid? accountId, string alertType, string severity, string description,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alertType);
        ArgumentException.ThrowIfNullOrWhiteSpace(severity);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        var alert = new RiskAlert
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            AlertType = alertType.Trim(),
            Severity = severity.Trim(),
            Description = description.Trim()
        };
        db.Alerts.Add(alert);
        await db.SaveChangesAsync(cancellationToken);
        return alert;
    }

    public async Task<ComplianceCase> OpenComplianceCaseAsync(
        Guid? clientId, Guid? accountId, string caseReference, string caseType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(caseType);
        if (clientId is null && accountId is null)
            throw new ArgumentException("A compliance case must reference a client or account.");
        var item = new ComplianceCase
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            AccountId = accountId,
            CaseReference = caseReference.Trim(),
            CaseType = caseType.Trim()
        };
        db.ComplianceCases.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    private static void ValidateCurrency(string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        if (currency.Trim().Length != 3)
            throw new ArgumentException("Currency must be a three-letter code.", nameof(currency));
    }
}
