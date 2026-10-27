using Brokerage.Common;

namespace Brokerage.Reconciliation;

public sealed class ReconciliationJsonRpcMethods : IJsonRpcMethodProvider
{
    public IEnumerable<JsonRpcMethod> GetMethods()
    {
        yield return new("reconciliation.start", async call =>
            await call.Service<ReconciliationService>().StartRunAsync(
                call.Required<string>("source"), call.Required<string>("runReference"),
                call.CancellationToken),
            RequiredRoles: ["BrokerageOperations", "Administrator"]);
        yield return new("reconciliation.complete", async call =>
        {
            await call.Service<ReconciliationService>().CompleteRunAsync(
                call.Required<Guid>("runId"), call.CancellationToken);
            return null;
        }, RequiredRoles: ["BrokerageOperations", "Administrator"]);
    }
}
