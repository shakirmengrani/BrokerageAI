using Brokerage.Common;

namespace Brokerage.Settlement;

public sealed class SettlementJsonRpcMethods : IJsonRpcMethodProvider
{
    public IEnumerable<JsonRpcMethod> GetMethods()
    {
        yield return new("settlement.createBatch", async call =>
            await call.Service<SettlementService>().CreateBatchAsync(
                call.Required<string>("batchReference"), call.Required<DateOnly>("settlementDate"),
                call.CancellationToken),
            RequiredRoles: ["BrokerageOperations", "Administrator"]);
        yield return new("settlement.markSettled", async call =>
        {
            await call.Service<SettlementService>().MarkSettledAsync(
                call.Required<Guid>("instructionId"), call.CancellationToken);
            return null;
        }, RequiredRoles: ["BrokerageOperations", "Administrator"]);
    }
}
