using Microsoft.EntityFrameworkCore;

namespace Brokerage.Payments;

public sealed class PaymentsService(PaymentsDb db)
{
    public async Task<Payment> CreatePaymentAsync(
        Guid accountId, string paymentReference, string idempotencyKey,
        PaymentDirection direction, decimal amount, string currency,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ValidateCurrency(currency);
        paymentReference = paymentReference.Trim();
        idempotencyKey = idempotencyKey.Trim();
        currency = currency.Trim().ToUpperInvariant();
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        var existing = await db.Payments.SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.AccountId == accountId && existing.Direction == direction
                && existing.Amount == amount && existing.Currency == currency)
                return existing;
            throw new InvalidOperationException("Idempotency key was already used for a different payment.");
        }

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            PaymentReference = paymentReference.Trim(),
            IdempotencyKey = idempotencyKey,
            Direction = direction,
            Amount = amount,
            Currency = currency
        };
        payment.Events.Add(new PaymentStatusEvent
        {
            Id = Guid.NewGuid(),
            Type = PaymentEventType.Created,
            Status = PaymentStatus.Pending
        });
        db.Payments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);
        return payment;
    }

    public async Task ChangeStatusAsync(
        Guid paymentId, PaymentStatus nextStatus, string? providerReference, string? providerCode,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var payment = await db.Payments.Include(x => x.Events)
            .SingleOrDefaultAsync(x => x.Id == paymentId, cancellationToken)
            ?? throw new KeyNotFoundException("Payment was not found.");
        if (!IsAllowedTransition(payment.Status, nextStatus))
            throw new InvalidOperationException($"Payment cannot transition from {payment.Status} to {nextStatus}.");

        var previousStatus = payment.Status;
        payment.Status = nextStatus;
        payment.ProviderReference = providerReference ?? payment.ProviderReference;
        if (nextStatus == PaymentStatus.Completed)
            payment.CompletedAt = DateTimeOffset.UtcNow;
        payment.Events.Add(new PaymentStatusEvent
        {
            Id = Guid.NewGuid(),
            Type = nextStatus == PaymentStatus.Reversed ? PaymentEventType.Reversed : PaymentEventType.StatusChanged,
            Status = nextStatus,
            ProviderCode = providerCode
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task<Payment?> GetPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default) =>
        db.Payments.AsNoTracking().Include(x => x.Events)
            .SingleOrDefaultAsync(x => x.Id == paymentId, cancellationToken);

    private static bool IsAllowedTransition(PaymentStatus current, PaymentStatus next) => current switch
    {
        PaymentStatus.Pending => next is PaymentStatus.Submitted or PaymentStatus.Cancelled,
        PaymentStatus.Submitted => next is PaymentStatus.Processing or PaymentStatus.Completed or PaymentStatus.Failed or PaymentStatus.Cancelled,
        PaymentStatus.Processing => next is PaymentStatus.Completed or PaymentStatus.Failed,
        PaymentStatus.Completed => next == PaymentStatus.Reversed,
        _ => false
    };

    private static void ValidateCurrency(string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        if (currency.Trim().Length != 3)
            throw new ArgumentException("Currency must be a three-letter code.", nameof(currency));
    }
}
