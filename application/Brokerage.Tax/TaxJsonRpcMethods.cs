using Brokerage.Common;

namespace Brokerage.Tax;

public sealed class TaxJsonRpcMethods : IJsonRpcMethodProvider
{
    public IEnumerable<JsonRpcMethod> GetMethods()
    {
        yield return new("tax.createStatement", async call =>
        {
            var accountId = call.Required<Guid>("accountId");
            call.RequireAccountAccess(accountId);
            return await call.Service<TaxService>().CreateStatementAsync(
                accountId, call.Required<int>("taxYear"), call.Required<string>("jurisdiction"),
                call.Required<string>("storageKey"), call.CancellationToken);
        }, RequiredRoles: ["BrokerageOperations", "Administrator"]);
    }
}
