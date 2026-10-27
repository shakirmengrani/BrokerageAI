using Brokerage.Common;

namespace Brokerage.Documents;

public sealed class DocumentsJsonRpcMethods : IJsonRpcMethodProvider
{
    public IEnumerable<JsonRpcMethod> GetMethods()
    {
        yield return new("documents.register", async call =>
        {
            var clientId = call.Optional<Guid>("clientId");
            if (clientId is not null)
                call.RequireClientAccess(clientId.Value);
            return await call.Service<DocumentsService>().RegisterDocumentAsync(
                clientId, call.Required<string>("documentType"), call.Required<string>("storageKey"),
                call.Required<string>("contentType"), call.Required<long>("sizeBytes"),
                call.OptionalString("sha256"), call.CancellationToken);
        }, RequiredRoles: ["BrokerageOperations", "Administrator"]);
    }
}
