using Microsoft.EntityFrameworkCore;

namespace Brokerage.Reconciliation;

public sealed class ReconciliationService(ReconciliationDb db)
{
    public async Task<ReconciliationRun> StartRunAsync(
        string source, string runReference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(runReference);
        var run = new ReconciliationRun
        {
            Id = Guid.NewGuid(),
            Source = source.Trim(),
            RunReference = runReference.Trim()
        };
        db.Runs.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return run;
    }

    public async Task<ReconciliationItem> AddItemAsync(
        Guid runId, string? internalReference, string? externalReference,
        decimal? internalAmount, decimal? externalAmount, string? currency,
        decimal tolerance = 0, CancellationToken cancellationToken = default)
    {
        if (tolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        if (internalReference is null && externalReference is null)
            throw new ArgumentException("A reconciliation item must reference an internal or external record.");
        var run = await db.Runs.SingleOrDefaultAsync(x => x.Id == runId, cancellationToken)
            ?? throw new KeyNotFoundException("Reconciliation run was not found.");
        if (run.Status != ReconciliationRunStatus.Running)
            throw new InvalidOperationException("Items can only be added to a running reconciliation.");
        if (internalAmount is not null && externalAmount is not null && string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Matched amounts require a currency.", nameof(currency));
        if (currency is not null && currency.Trim().Length != 3)
            throw new ArgumentException("Currency must be a three-letter code.", nameof(currency));

        var status = internalReference is null ? ReconciliationItemStatus.UnmatchedExternal
            : externalReference is null ? ReconciliationItemStatus.UnmatchedInternal
            : internalAmount is null || externalAmount is null
                ? ReconciliationItemStatus.Mismatched
            : Math.Abs(internalAmount.Value - externalAmount.Value) <= tolerance
                ? ReconciliationItemStatus.Matched : ReconciliationItemStatus.Mismatched;
        var item = new ReconciliationItem
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Status = status,
            InternalReference = internalReference,
            ExternalReference = externalReference,
            InternalAmount = internalAmount,
            ExternalAmount = externalAmount,
            Currency = currency?.Trim().ToUpperInvariant()
        };
        db.Items.Add(item);
        if (status != ReconciliationItemStatus.Matched)
        {
            db.Exceptions.Add(new ReconciliationException
            {
                Id = Guid.NewGuid(),
                ItemId = item.Id,
                ReasonCode = status.ToString().ToUpperInvariant(),
                Description = $"Reconciliation item requires review: {status}."
            });
        }
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task CompleteRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await db.Runs.SingleOrDefaultAsync(x => x.Id == runId, cancellationToken)
            ?? throw new KeyNotFoundException("Reconciliation run was not found.");
        if (run.Status != ReconciliationRunStatus.Running)
            throw new InvalidOperationException("Only running reconciliations can be completed.");
        var hasExceptions = await db.Items.AnyAsync(
            x => x.RunId == runId && x.Status != ReconciliationItemStatus.Matched, cancellationToken);
        run.Status = hasExceptions
            ? ReconciliationRunStatus.CompletedWithExceptions
            : ReconciliationRunStatus.Completed;
        run.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResolveExceptionAsync(
        Guid exceptionId, bool waive, CancellationToken cancellationToken = default)
    {
        var item = await db.Exceptions.SingleOrDefaultAsync(x => x.Id == exceptionId, cancellationToken)
            ?? throw new KeyNotFoundException("Reconciliation exception was not found.");
        if (item.Status != ReconciliationExceptionStatus.Open)
            throw new InvalidOperationException("Only open reconciliation exceptions can be resolved.");
        item.Status = waive ? ReconciliationExceptionStatus.Waived : ReconciliationExceptionStatus.Resolved;
        item.ResolvedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
