using Brokerage.Common;

namespace Brokerage.Portfolios;

public sealed class PortfoliosJsonRpcMethods : IJsonRpcMethodProvider
{
    public IEnumerable<JsonRpcMethod> GetMethods()
    {
        yield return new("portfolios.create", async call =>
        {
            var clientId = call.Required<Guid>("clientId");
            call.RequireClientAccess(clientId);
            return await call.Service<PortfoliosService>().CreatePortfolioAsync(
                clientId, call.Required<string>("name"), call.Required<string>("baseCurrency"),
                call.CancellationToken);
        });
        yield return new("portfolios.positions", async call =>
        {
            var clientId = call.Required<Guid>("clientId");
            call.RequireClientAccess(clientId);
            return await call.Service<PortfoliosService>().GetPositionsForClientAsync(
                clientId, call.Required<Guid>("portfolioId"), call.CancellationToken);
        });
    }
}
