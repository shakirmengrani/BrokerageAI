using System.Security.Claims;
using Brokerage.Accounts;
using Brokerage.MarketData;

namespace Brokerage.Mcp;

public sealed class BrokerageToolService(AccountsService accounts, MarketDataService marketData)
{
    public async Task<AccountBalanceResult> GetAccountBalanceAsync(
        ClaimsPrincipal user,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        RequireAuthenticatedUser(user);
        RequireAccountAccess(user, accountId);

        var balance = await accounts.GetBalanceAsync(accountId, cancellationToken);
        return new AccountBalanceResult(accountId, balance);
    }

    public async Task<LatestQuoteResult?> GetLatestQuoteAsync(
        ClaimsPrincipal user,
        Guid instrumentId,
        CancellationToken cancellationToken)
    {
        RequireAuthenticatedUser(user);

        var quote = await marketData.GetLatestQuoteAsync(instrumentId, cancellationToken);
        return quote is null
            ? null
            : new LatestQuoteResult(quote.InstrumentId, quote.Bid, quote.Ask, quote.ObservedAt);
    }

    private static void RequireAuthenticatedUser(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("Authentication is required.");
    }

    private static void RequireAccountAccess(ClaimsPrincipal user, Guid accountId)
    {
        if (user.IsInRole("Administrator") || user.IsInRole("BrokerageOperations"))
            return;

        if (!user.FindAll("account_id").Any(claim =>
                Guid.TryParse(claim.Value, out var claimAccountId) && claimAccountId == accountId))
            throw new UnauthorizedAccessException("The caller is not authorized to access this account.");
    }
}

public sealed record AccountBalanceResult(Guid AccountId, decimal Balance);

public sealed record LatestQuoteResult(
    Guid InstrumentId,
    decimal Bid,
    decimal Ask,
    DateTimeOffset ObservedAt);
