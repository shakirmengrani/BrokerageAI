using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Orders;

public sealed class OrdersService(OrdersDb db)
{
    public async Task<Guid> GetOrderAccountIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        await db.Orders.AsNoTracking()
            .Where(x => x.Id == orderId)
            .Select(x => (Guid?)x.AccountId)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new KeyNotFoundException("Order was not found.");

    public async Task<Order> PlaceOrderAsync(
        Guid accountId, Guid instrumentId, string clientOrderReference, OrderSide side, OrderType type,
        TimeInForce timeInForce, decimal quantity, decimal? limitPrice,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientOrderReference);
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        if (type is OrderType.Limit or OrderType.StopLimit && limitPrice is null or <= 0)
            throw new ArgumentException("Limit and stop-limit orders require a positive limit price.", nameof(limitPrice));

        var order = new Order
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            InstrumentId = instrumentId,
            ClientOrderReference = clientOrderReference.Trim(),
            Side = side,
            Type = type,
            TimeInForce = timeInForce,
            Quantity = quantity,
            LimitPrice = limitPrice
        };
        order.Events.Add(CreateEvent("placed", new { order.Id, order.Status }));
        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        return order;
    }

    public async Task CancelOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken)
            ?? throw new KeyNotFoundException("Order was not found.");
        if (order.Status is not (OrderStatus.Pending or OrderStatus.Accepted or OrderStatus.PartiallyFilled))
            throw new InvalidOperationException("This order can no longer be cancelled.");
        order.Status = OrderStatus.Cancelled;
        order.Events.Add(CreateEvent("cancelled", new { order.Id }));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Execution> RecordExecutionAsync(
        Guid orderId, string executionReference, decimal quantity, decimal price,
        DateTimeOffset executedAt, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionReference);
        if (quantity <= 0 || price <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Execution quantity and price must be positive.");
        if (executedAt > DateTimeOffset.UtcNow)
            throw new ArgumentOutOfRangeException(nameof(executedAt), "Execution time cannot be in the future.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var order = await db.Orders.Include(x => x.Executions)
            .SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken)
            ?? throw new KeyNotFoundException("Order was not found.");
        if (order.Status is not (OrderStatus.Accepted or OrderStatus.PartiallyFilled))
            throw new InvalidOperationException("Executions can only be recorded for accepted, open orders.");
        if (await db.Executions.AnyAsync(x => x.ExecutionReference == executionReference, cancellationToken))
            throw new InvalidOperationException("Execution reference already exists.");
        var filledQuantity = order.Executions.Sum(x => x.Quantity);
        if (filledQuantity + quantity > order.Quantity)
            throw new InvalidOperationException("Execution quantity exceeds the remaining order quantity.");

        var execution = new Execution
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ExecutionReference = executionReference.Trim(),
            Quantity = quantity,
            Price = price,
            ExecutedAt = executedAt
        };
        order.Executions.Add(execution);
        order.Status = filledQuantity + quantity == order.Quantity ? OrderStatus.Filled : OrderStatus.PartiallyFilled;
        order.Events.Add(CreateEvent("execution_recorded", new { execution.Id, execution.Quantity, execution.Price, order.Status }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return execution;
    }

    public async Task AcceptOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken)
            ?? throw new KeyNotFoundException("Order was not found.");
        if (order.Status != OrderStatus.Pending)
            throw new InvalidOperationException("Only pending orders can be accepted.");
        order.Status = OrderStatus.Accepted;
        order.Events.Add(CreateEvent("accepted", new { order.Id }));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static OrderEvent CreateEvent(string type, object payload) =>
        new() { Id = Guid.NewGuid(), EventType = type, PayloadJson = JsonSerializer.Serialize(payload) };
}
