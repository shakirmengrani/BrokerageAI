using Brokerage.Common;

namespace Brokerage.Notifications;

public sealed class NotificationsJsonRpcMethods : IJsonRpcMethodProvider
{
    public IEnumerable<JsonRpcMethod> GetMethods()
    {
        yield return new("notifications.enqueue", async call =>
        {
            var clientId = call.Optional<Guid>("clientId");
            if (clientId is not null)
                call.RequireClientAccess(clientId.Value);
            return await call.Service<NotificationsService>().EnqueueAsync(
                clientId, call.Required<string>("channel"), call.Required<string>("destination"),
                call.Required<string>("template"), call.OptionalString("payloadJson"),
                call.CancellationToken);
        }, RequiredRoles: ["BrokerageOperations", "Administrator"]);
        yield return new("notifications.queued", async call =>
            await call.Service<NotificationsService>().GetQueuedAsync(
                call.Optional<int>("limit") ?? 100, call.CancellationToken),
            RequiredRoles: ["BrokerageOperations", "Administrator"]);
    }
}
