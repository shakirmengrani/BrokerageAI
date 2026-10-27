using Brokerage.Common;

namespace Brokerage.Integrations;

public sealed class IntegrationsJsonRpcMethods : IJsonRpcMethodProvider
{
    public IEnumerable<JsonRpcMethod> GetMethods()
    {
        yield return new("integrations.enqueue", async call =>
            await call.Service<IntegrationsService>().EnqueueMessageAsync(
                call.Required<Guid>("endpointId"), call.Required<string>("messageReference"),
                call.Required<string>("messageType"), call.Required<IntegrationMessageDirection>("direction"),
                call.OptionalString("payloadJson"), call.CancellationToken),
            RequiredRoles: ["BrokerageOperations", "Administrator"]);
    }
}
