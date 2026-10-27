using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Brokerage.Common;

public delegate Task<object?> JsonRpcHandler(JsonRpcCall call);

public sealed record JsonRpcMethod(
    string Name,
    JsonRpcHandler Handler,
    bool RequiresAuthentication = true,
    string[]? RequiredRoles = null);

public interface IJsonRpcMethodProvider
{
    IEnumerable<JsonRpcMethod> GetMethods();
}

public sealed class JsonRpcCall(
    JsonObject parameters,
    IServiceProvider services,
    ClaimsPrincipal user,
    CancellationToken cancellationToken)
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public JsonObject Parameters { get; } = parameters;
    public ClaimsPrincipal User { get; } = user;
    public CancellationToken CancellationToken { get; } = cancellationToken;

    public T Required<T>(string name)
    {
        if (!Parameters.TryGetPropertyValue(name, out var node) || node is null)
            throw new JsonRpcInvalidParamsException($"Missing required parameter '{name}'.");
        try
        {
            return node.Deserialize<T>(SerializerOptions)
                ?? throw new JsonRpcInvalidParamsException($"Parameter '{name}' cannot be null.");
        }
        catch (JsonException)
        {
            throw new JsonRpcInvalidParamsException($"Parameter '{name}' has an invalid value.");
        }
    }

    public T? Optional<T>(string name) where T : struct
    {
        if (!Parameters.TryGetPropertyValue(name, out var node) || node is null)
            return null;
        try
        {
            return node.Deserialize<T>(SerializerOptions);
        }
        catch (JsonException)
        {
            throw new JsonRpcInvalidParamsException($"Parameter '{name}' has an invalid value.");
        }
    }

    public string? OptionalString(string name)
    {
        if (!Parameters.TryGetPropertyValue(name, out var node) || node is null)
            return null;
        try
        {
            return node.Deserialize<string>(SerializerOptions)
                ?? throw new JsonRpcInvalidParamsException($"Parameter '{name}' cannot be null.");
        }
        catch (JsonException)
        {
            throw new JsonRpcInvalidParamsException($"Parameter '{name}' has an invalid value.");
        }
    }

    public TService Service<TService>() where TService : notnull =>
        services.GetService(typeof(TService)) is TService service
            ? service
            : throw new InvalidOperationException($"Service '{typeof(TService).Name}' is not registered.");

    public string ActorId =>
        User.FindFirst("sub")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new UnauthorizedAccessException("Authenticated principal is missing a stable user identifier.");

    public void RequireRole(params string[] roles)
    {
        if (!roles.Any(User.IsInRole))
            throw new UnauthorizedAccessException("Caller does not have the required role.");
    }

    public void RequireAccountAccess(Guid accountId)
    {
        if (User.IsInRole("Administrator") || User.IsInRole("BrokerageOperations"))
            return;
        if (!User.FindAll("account_id").Any(claim =>
                Guid.TryParse(claim.Value, out var claimAccountId) && claimAccountId == accountId))
            throw new UnauthorizedAccessException("Caller is not authorized for this account.");
    }

    public void RequireClientAccess(Guid clientId)
    {
        if (User.IsInRole("Administrator") || User.IsInRole("ComplianceOfficer"))
            return;
        if (!User.FindAll("client_id").Any(claim =>
                Guid.TryParse(claim.Value, out var claimClientId) && claimClientId == clientId))
            throw new UnauthorizedAccessException("Caller is not authorized for this client.");
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public sealed class JsonRpcInvalidParamsException(string message) : Exception(message)
{
}
