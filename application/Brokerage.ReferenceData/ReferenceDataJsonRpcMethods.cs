using Brokerage.Common;

namespace Brokerage.ReferenceData;

public sealed class ReferenceDataJsonRpcMethods : IJsonRpcMethodProvider
{
    public IEnumerable<JsonRpcMethod> GetMethods()
    {
        yield return new("referenceData.isBusinessDay", async call =>
            await call.Service<ReferenceDataService>().IsBusinessDayAsync(
                call.Required<Guid>("calendarId"), call.Required<DateOnly>("date"), call.CancellationToken));
        yield return new("referenceData.setCurrency", async call =>
            await call.Service<ReferenceDataService>().SetCurrencyAsync(
                call.Required<string>("code"), call.Required<string>("name"),
                call.Required<int>("minorUnits"), call.CancellationToken),
            RequiredRoles: ["ReferenceDataManager", "Administrator"]);
    }
}
