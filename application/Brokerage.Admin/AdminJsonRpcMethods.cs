using Brokerage.Common;

namespace Brokerage.Admin;

public sealed class AdminJsonRpcMethods : IJsonRpcMethodProvider
{
    public IEnumerable<JsonRpcMethod> GetMethods()
    {
        yield return new("admin.setOperationalControl", async call =>
            await call.Service<AdminService>().SetOperationalControlAsync(
                call.Required<string>("controlKey"), call.Required<bool>("isEnabled"),
                call.OptionalString("reason"), call.ActorId, call.CancellationToken),
            RequiredRoles: ["Administrator"]);
        yield return new("admin.operationalControls", async call =>
            await call.Service<AdminService>().GetOperationalControlsAsync(call.CancellationToken),
            RequiredRoles: ["Administrator"]);
    }
}
