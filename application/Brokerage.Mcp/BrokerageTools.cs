using System.ComponentModel;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Brokerage.Mcp;

[McpServerToolType]
public sealed class BrokerageTools
{
    [McpServerTool(
        Name = "accounts_get_balance",
        Title = "Get account balance",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false)]
    [Description("Returns the posted ledger balance for an account the authenticated caller is authorized to access.")]
    public static async Task<AccountBalanceResult> GetAccountBalance(
        BrokerageToolService brokerageTools,
        IHttpContextAccessor httpContextAccessor,
        [Description("The brokerage account identifier. Access is checked against the caller's trusted claims.")]
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var user = RequireAuthenticatedUser(httpContextAccessor);
        try
        {
            return await brokerageTools.GetAccountBalanceAsync(user, accountId, cancellationToken);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new McpException(exception.Message);
        }
    }

    [McpServerTool(
        Name = "market_data_get_latest_quote",
        Title = "Get latest market quote",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false)]
    [Description("Returns the most recent recorded bid and ask for an instrument. This is stored quote data, not a live price guarantee or investment advice.")]
    public static async Task<LatestQuoteResult?> GetLatestQuote(
        BrokerageToolService brokerageTools,
        IHttpContextAccessor httpContextAccessor,
        [Description("The instrument identifier to look up.")]
        Guid instrumentId,
        CancellationToken cancellationToken)
    {
        var user = RequireAuthenticatedUser(httpContextAccessor);
        try
        {
            return await brokerageTools.GetLatestQuoteAsync(user, instrumentId, cancellationToken);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new McpException(exception.Message);
        }
    }

    private static System.Security.Claims.ClaimsPrincipal RequireAuthenticatedUser(
        IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;
        return user?.Identity?.IsAuthenticated == true
            ? user
            : throw new McpException("Authentication is required.");
    }
}
