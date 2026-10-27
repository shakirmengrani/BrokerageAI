namespace Brokerage.Orders;

public enum OrderSide
{
    Buy,
    Sell
}

public enum OrderType
{
    Market,
    Limit,
    Stop,
    StopLimit
}

public enum TimeInForce
{
    Day,
    GoodTillCanceled,
    ImmediateOrCancel,
    FillOrKill
}

public enum OrderStatus
{
    Pending,
    Accepted,
    PartiallyFilled,
    Filled,
    Cancelled,
    Rejected,
    Expired
}

public sealed class Order
{
    public Guid Id { get; init; }
    public Guid AccountId { get; init; }
    public Guid InstrumentId { get; init; }
    public required string ClientOrderReference { get; set; }
    public OrderSide Side { get; set; }
    public OrderType Type { get; set; }
    public TimeInForce TimeInForce { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal Quantity { get; set; }
    public decimal? LimitPrice { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public ICollection<Execution> Executions { get; } = [];
    public ICollection<OrderEvent> Events { get; } = [];
}

public sealed class Execution
{
    public Guid Id { get; init; }
    public Guid OrderId { get; init; }
    public required string ExecutionReference { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public DateTimeOffset ExecutedAt { get; set; }
    public Order Order { get; init; } = null!;
}

public sealed class OrderEvent
{
    public Guid Id { get; init; }
    public Guid OrderId { get; init; }
    public required string EventType { get; set; }
    public required string PayloadJson { get; set; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public Order Order { get; init; } = null!;
}
