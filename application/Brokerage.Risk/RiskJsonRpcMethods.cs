using Brokerage.Common;

namespace Brokerage.Risk;

public sealed class RiskJsonRpcMethods : IJsonRpcMethodProvider
{
    public IEnumerable<JsonRpcMethod> GetMethods()
    {
        yield return new("risk.checkLimit", async call =>
        {
            var accountId = call.Required<Guid>("accountId");
            call.RequireAccountAccess(accountId);
            return await call.Service<RiskService>().IsWithinLimitAsync(
                accountId, call.Required<string>("limitType"), call.Required<decimal>("proposedValue"),
                call.Required<string>("currency"), call.CancellationToken);
        });
        yield return new("risk.createAlert", async call =>
            await call.Service<RiskService>().CreateAlertAsync(
                call.Optional<Guid>("accountId"), call.Required<string>("alertType"),
                call.Required<string>("severity"), call.Required<string>("description"),
                call.CancellationToken),
            RequiredRoles: ["RiskOfficer", "Administrator"]);
    }
}
