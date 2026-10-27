using Brokerage.Common;

namespace Brokerage.CorporateActions;

public sealed class CorporateActionsJsonRpcMethods : IJsonRpcMethodProvider
{
    public IEnumerable<JsonRpcMethod> GetMethods()
    {
        yield return new("corporateActions.recordElection", async call =>
        {
            var accountId = call.Required<Guid>("accountId");
            call.RequireAccountAccess(accountId);
            return await call.Service<CorporateActionsService>().RecordElectionAsync(
                call.Required<Guid>("actionId"), accountId, call.Required<string>("electionOption"),
                call.Optional<decimal>("quantity"), call.CancellationToken);
        });
    }
}
