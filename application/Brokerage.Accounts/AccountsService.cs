using Microsoft.EntityFrameworkCore;

namespace Brokerage.Accounts;

public sealed record LedgerEntryInput(LedgerEntryType Type, decimal Amount, string Currency);

public sealed class AccountsService(AccountsDb db)
{
    public async Task<TradingAccount> OpenAccountAsync(
        Guid clientId, string accountNumber, string currency, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountNumber);
        ValidateCurrency(currency);
        if (await db.Accounts.AnyAsync(x => x.AccountNumber == accountNumber, cancellationToken))
            throw new InvalidOperationException("An account with this number already exists.");

        var account = new TradingAccount
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            AccountNumber = accountNumber.Trim(),
            Currency = currency.ToUpperInvariant(),
            Status = AccountStatus.Pending
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);
        return account;
    }

    public async Task<Guid> CreateJournalAsync(
        Guid accountId, string reference, IReadOnlyCollection<LedgerEntryInput> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count < 2 || entries.Any(x => x.Amount <= 0))
            throw new ArgumentException("A journal requires at least two positive ledger entries.", nameof(entries));
        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == accountId, cancellationToken)
            ?? throw new KeyNotFoundException("Account was not found.");
        if (account.Status != AccountStatus.Active)
            throw new InvalidOperationException("Journal entries can only be created for active accounts.");
        if (entries.Any(x => !string.Equals(x.Currency, account.Currency, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("All journal entries must use the account currency.", nameof(entries));

        var journal = new JournalEntry { Id = Guid.NewGuid(), AccountId = accountId, Reference = reference.Trim() };
        foreach (var entry in entries)
            journal.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                Type = entry.Type,
                Amount = entry.Amount,
                Currency = entry.Currency.ToUpperInvariant()
            });
        db.JournalEntries.Add(journal);
        await db.SaveChangesAsync(cancellationToken);
        return journal.Id;
    }

    public async Task PostJournalAsync(Guid journalId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var journal = await db.JournalEntries.Include(x => x.LedgerEntries)
            .SingleOrDefaultAsync(x => x.Id == journalId, cancellationToken)
            ?? throw new KeyNotFoundException("Journal entry was not found.");
        if (journal.Status != JournalStatus.Draft)
            throw new InvalidOperationException("Only draft journals can be posted.");
        var totals = journal.LedgerEntries
            .GroupBy(x => x.Currency, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Debits = g.Where(x => x.Type == LedgerEntryType.Debit).Sum(x => x.Amount),
                Credits = g.Where(x => x.Type == LedgerEntryType.Credit).Sum(x => x.Amount)
            });
        if (journal.LedgerEntries.Count < 2 || totals.Any(x => x.Debits != x.Credits))
            throw new InvalidOperationException("Journal debits and credits must balance for each currency before posting.");

        journal.Status = JournalStatus.Posted;
        journal.PostedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<decimal> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == accountId, cancellationToken)
            ?? throw new KeyNotFoundException("Account was not found.");
        var postedEntries = db.LedgerEntries.AsNoTracking()
            .Where(x => x.JournalEntry.AccountId == accountId && x.JournalEntry.Status == JournalStatus.Posted
                && x.Currency == account.Currency);
        var debits = await postedEntries.Where(x => x.Type == LedgerEntryType.Debit).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
        var credits = await postedEntries.Where(x => x.Type == LedgerEntryType.Credit).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
        return debits - credits;
    }

    private static void ValidateCurrency(string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        if (currency.Trim().Length != 3)
            throw new ArgumentException("Currency must be a three-letter code.", nameof(currency));
    }
}
