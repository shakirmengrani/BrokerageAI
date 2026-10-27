using Brokerage.Common;

namespace Brokerage.Billing;

public sealed class BillingJsonRpcMethods : IJsonRpcMethodProvider
{
    public IEnumerable<JsonRpcMethod> GetMethods()
    {
        yield return new("billing.calculateCommission", async call =>
            await call.Service<BillingService>().CalculateCommissionAsync(
                call.Required<Guid>("feeScheduleId"), call.Required<string>("instrumentType"),
                call.Required<decimal>("notional"), call.CancellationToken));
        yield return new("billing.createCharge", async call =>
        {
            var accountId = call.Required<Guid>("accountId");
            call.RequireAccountAccess(accountId);
            return await call.Service<BillingService>().CreateChargeAsync(
                accountId, call.Optional<Guid>("orderId"), call.Required<string>("reference"),
                call.Required<string>("chargeType"), call.Required<decimal>("amount"),
                call.Required<string>("currency"), call.CancellationToken);
        }, RequiredRoles: ["BrokerageOperations", "Administrator"]);
    }
}
