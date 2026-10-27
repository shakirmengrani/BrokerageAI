using Brokerage.Common;

namespace Brokerage.MarketData;

public sealed class MarketDataJsonRpcMethods : IJsonRpcMethodProvider
{
    public IEnumerable<JsonRpcMethod> GetMethods()
    {
        yield return new("marketData.latestQuote", async call =>
            await call.Service<MarketDataService>().GetLatestQuoteAsync(
                call.Required<Guid>("instrumentId"), call.CancellationToken));
        yield return new("marketData.recordQuote", async call =>
            await call.Service<MarketDataService>().RecordQuoteAsync(
                call.Required<Guid>("instrumentId"), call.Required<decimal>("bid"),
                call.Required<decimal>("ask"), call.Required<DateTimeOffset>("observedAt"),
                call.CancellationToken),
            RequiredRoles: ["MarketDataProvider", "Administrator"]);
    }
}
