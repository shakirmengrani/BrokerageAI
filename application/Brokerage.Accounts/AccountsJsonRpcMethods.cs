using Brokerage.Common;

namespace Brokerage.Accounts;

public sealed class AccountsJsonRpcMethods : IJsonRpcMethodProvider
{
    public IEnumerable<JsonRpcMethod> GetMethods()
    {
        yield return new("accounts.open", async call =>
            await call.Service<AccountsService>().OpenAccountAsync(
                call.Required<Guid>("clientId"), call.Required<string>("accountNumber"),
                call.Required<string>("currency"), call.CancellationToken),
            RequiredRoles: ["Administrator"]);
        yield return new("accounts.balance", async call =>
        {
            var accountId = call.Required<Guid>("accountId");
            call.RequireAccountAccess(accountId);
            return await call.Service<AccountsService>().GetBalanceAsync(accountId, call.CancellationToken);
        });
        yield return new("accounts.createJournal", async call =>
        {
            call.RequireRole("BrokerageOperations", "Administrator");
            var accountId = call.Required<Guid>("accountId");
            call.RequireAccountAccess(accountId);
            return await call.Service<AccountsService>().CreateJournalAsync(
                accountId, call.Required<string>("reference"),
                call.Required<List<LedgerEntryInput>>("entries"), call.CancellationToken);
        });
        yield return new("accounts.postJournal", async call =>
        {
            call.RequireRole("BrokerageOperations", "Administrator");
            await call.Service<AccountsService>().PostJournalAsync(
                call.Required<Guid>("journalId"), call.CancellationToken);
            return null;
        }, RequiredRoles: ["BrokerageOperations", "Administrator"]);
    }
}
