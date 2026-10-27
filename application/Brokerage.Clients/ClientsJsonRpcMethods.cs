using Brokerage.Common;

namespace Brokerage.Clients;

public sealed class ClientsJsonRpcMethods : IJsonRpcMethodProvider
{
    public IEnumerable<JsonRpcMethod> GetMethods()
    {
        yield return new("clients.register", async call =>
            await call.Service<ClientsService>().RegisterClientAsync(
                call.Required<string>("externalReference"), call.Required<string>("givenName"),
                call.Required<string>("familyName"), call.Required<string>("email"), call.CancellationToken));
        yield return new("clients.startKyc", async call =>
        {
            var clientId = call.Required<Guid>("clientId");
            call.RequireClientAccess(clientId);
            return await call.Service<ClientsService>().StartKycAsync(clientId, call.CancellationToken);
        });
        yield return new("clients.reviewKyc", async call =>
        {
            await call.Service<ClientsService>().ReviewKycAsync(
                call.Required<Guid>("recordId"), call.Required<KycStatus>("decision"),
                call.ActorId, call.CancellationToken);
            return null;
        }, RequiredRoles: ["ComplianceOfficer", "Administrator"]);
        yield return new("clients.get", async call =>
        {
            var clientId = call.Required<Guid>("clientId");
            call.RequireClientAccess(clientId);
            return await call.Service<ClientsService>().GetClientAsync(clientId, call.CancellationToken);
        });
    }
}
