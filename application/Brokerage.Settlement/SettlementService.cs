using Microsoft.EntityFrameworkCore;

namespace Brokerage.Settlement;

public sealed class SettlementService(SettlementDb db)
{
    public async Task<SettlementBatch> CreateBatchAsync(
        string batchReference, DateOnly settlementDate, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(batchReference);
        if (await db.Batches.AnyAsync(x => x.BatchReference == batchReference, cancellationToken))
            throw new InvalidOperationException("A settlement batch with this reference already exists.");
        var batch = new SettlementBatch
        {
            Id = Guid.NewGuid(),
            BatchReference = batchReference.Trim(),
            SettlementDate = settlementDate
        };
        db.Batches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);
        return batch;
    }

    public async Task<SettlementInstruction> AddInstructionAsync(
        Guid batchId, Guid accountId, Guid instrumentId, SettlementDirection direction,
        decimal quantity, decimal cashAmount, string currency, DateOnly settlementDate,
        string externalReference, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0 || cashAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive and cash amount non-negative.");
        ValidateCurrency(currency);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalReference);
        var batch = await db.Batches.SingleOrDefaultAsync(x => x.Id == batchId, cancellationToken)
            ?? throw new KeyNotFoundException("Settlement batch was not found.");
        if (batch.SettlementDate != settlementDate)
            throw new ArgumentException("Instruction settlement date must match its batch.", nameof(settlementDate));
        if (await db.Instructions.AnyAsync(x => x.ExternalReference == externalReference, cancellationToken))
            throw new InvalidOperationException("External instruction reference already exists.");
        var instruction = new SettlementInstruction
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            AccountId = accountId,
            InstrumentId = instrumentId,
            Direction = direction,
            Quantity = quantity,
            CashAmount = cashAmount,
            Currency = currency.ToUpperInvariant(),
            SettlementDate = settlementDate,
            ExternalReference = externalReference.Trim()
        };
        db.Instructions.Add(instruction);
        await db.SaveChangesAsync(cancellationToken);
        return instruction;
    }

    public async Task MarkSettledAsync(Guid instructionId, CancellationToken cancellationToken = default)
    {
        var instruction = await db.Instructions.SingleOrDefaultAsync(x => x.Id == instructionId, cancellationToken)
            ?? throw new KeyNotFoundException("Settlement instruction was not found.");
        if (instruction.Status != SettlementStatus.Pending)
            throw new InvalidOperationException("Only pending settlement instructions can be settled.");
        instruction.Status = SettlementStatus.Settled;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SettlementFailure> RecordFailureAsync(
        Guid instructionId, string reasonCode, string description, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        var instruction = await db.Instructions.SingleOrDefaultAsync(x => x.Id == instructionId, cancellationToken)
            ?? throw new KeyNotFoundException("Settlement instruction was not found.");
        if (instruction.Status is SettlementStatus.Settled or SettlementStatus.Cancelled)
            throw new InvalidOperationException("Settled or cancelled instructions cannot be marked failed.");
        instruction.Status = SettlementStatus.Failed;
        var failure = new SettlementFailure
        {
            Id = Guid.NewGuid(),
            InstructionId = instructionId,
            ReasonCode = reasonCode.Trim(),
            Description = description.Trim()
        };
        db.Failures.Add(failure);
        await db.SaveChangesAsync(cancellationToken);
        return failure;
    }

    private static void ValidateCurrency(string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        if (currency.Trim().Length != 3)
            throw new ArgumentException("Currency must be a three-letter code.", nameof(currency));
    }
}
