using Brokerage.Common;

namespace Brokerage.Identity;

public sealed class IdentityJsonRpcMethods : IJsonRpcMethodProvider
{
    public IEnumerable<JsonRpcMethod> GetMethods()
    {
        yield return new("identity.createRole", async call =>
            await call.Service<IdentityService>().CreateRoleAsync(
                call.Required<string>("name"), call.CancellationToken),
            RequiredRoles: ["Administrator"]);
        yield return new("identity.assignRole", async call =>
        {
            await call.Service<IdentityService>().AssignRoleAsync(
                call.Required<Guid>("userId"), call.Required<Guid>("roleId"), call.CancellationToken);
            return null;
        }, RequiredRoles: ["Administrator"]);
    }
}
