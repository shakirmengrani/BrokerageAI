namespace Brokerage.Accounts;

public enum AccountStatus
{
    Pending,
    Active,
    Restricted,
    Closed
}

public enum JournalStatus
{
    Draft,
    Posted,
    Reversed
}

public enum LedgerEntryType
{
    Debit,
    Credit
}

public enum CashMovementDirection
{
    Deposit,
    Withdrawal
}

public enum CashMovementStatus
{
    Pending,
    Completed,
    Failed,
    Reversed
}

public sealed class TradingAccount
{
    public Guid Id { get; init; }
    public Guid ClientId { get; init; }
    public required string AccountNumber { get; set; }
    public required string Currency { get; set; }
    public AccountStatus Status { get; set; } = AccountStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public ICollection<JournalEntry> JournalEntries { get; } = [];
    public ICollection<CashMovement> CashMovements { get; } = [];
}

public sealed class JournalEntry
{
    public Guid Id { get; init; }
    public Guid AccountId { get; init; }
    public required string Reference { get; set; }
    public JournalStatus Status { get; set; } = JournalStatus.Draft;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PostedAt { get; set; }
    public TradingAccount Account { get; init; } = null!;
    public ICollection<LedgerEntry> LedgerEntries { get; } = [];
}

public sealed class LedgerEntry
{
    public Guid Id { get; init; }
    public Guid JournalEntryId { get; init; }
    public LedgerEntryType Type { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public JournalEntry JournalEntry { get; init; } = null!;
}

public sealed class CashMovement
{
    public Guid Id { get; init; }
    public Guid AccountId { get; init; }
    public required string ExternalReference { get; set; }
    public CashMovementDirection Direction { get; set; }
    public CashMovementStatus Status { get; set; } = CashMovementStatus.Pending;
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public TradingAccount Account { get; init; } = null!;
}
