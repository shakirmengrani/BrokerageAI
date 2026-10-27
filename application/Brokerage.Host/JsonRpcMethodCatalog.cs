using Brokerage.Common;

namespace Brokerage.Host;

public sealed class JsonRpcMethodCatalog(IEnumerable<IJsonRpcMethodProvider> providers)
{
    private readonly IReadOnlyDictionary<string, JsonRpcMethod> _methods = BuildMethods(providers);

    public IEnumerable<JsonRpcMethod> Methods => _methods.Values;

    public bool TryGetMethod(string name, out JsonRpcMethod method) =>
        _methods.TryGetValue(name, out method!);

    private static IReadOnlyDictionary<string, JsonRpcMethod> BuildMethods(
        IEnumerable<IJsonRpcMethodProvider> providers)
    {
        var methods = new Dictionary<string, JsonRpcMethod>(StringComparer.Ordinal)
        {
            ["system.ping"] = new(
                "system.ping",
                _ => Task.FromResult<object?>(new { status = "ok" }),
                RequiresAuthentication: false)
        };

        foreach (var provider in providers)
            foreach (var method in provider.GetMethods())
            {
                if (!methods.TryAdd(method.Name, method))
                    throw new InvalidOperationException($"Duplicate JSON-RPC method registration: '{method.Name}'.");
            }

        return methods;
    }
}
