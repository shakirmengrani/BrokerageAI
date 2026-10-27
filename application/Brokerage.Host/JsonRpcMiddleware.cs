using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Brokerage.Common;

namespace Brokerage.Host;

public sealed class JsonRpcMiddleware(
    RequestDelegate next,
    ILogger<JsonRpcMiddleware> logger,
    JsonRpcMethodCatalog methods)
{
    private const int MaximumBodyBytes = 1_048_576;
    private const int MaximumBatchSize = 20;
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.Equals("/rpc", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (!HttpMethods.IsPost(context.Request.Method))
        {
            context.Response.Headers.Allow = HttpMethods.Post;
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        if (!context.Request.HasJsonContentType())
        {
            context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
            return;
        }

        if (context.Request.ContentLength > MaximumBodyBytes)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        JsonNode? request;
        try
        {
            request = await ParseBoundedAsync(context.Request.Body, context.RequestAborted);
        }
        catch (RequestTooLargeException)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }
        catch (JsonException)
        {
            await WriteResponseAsync(context, ErrorResponse(null, -32700, "Parse error"));
            return;
        }

        if (request is JsonArray batch)
        {
            if (batch.Count == 0 || batch.Count > MaximumBatchSize)
            {
                await WriteResponseAsync(context, ErrorResponse(null, -32600, "Invalid Request"));
                return;
            }

            var responses = new JsonArray();
            foreach (var entry in batch)
            {
                var response = await ProcessAsync(entry, context);
                if (response is not null)
                    responses.Add(response);
            }

            if (responses.Count == 0)
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }
            await WriteResponseAsync(context, responses);
            return;
        }

        var singleResponse = await ProcessAsync(request, context);
        if (singleResponse is null)
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }
        await WriteResponseAsync(context, singleResponse);
    }

    private async Task<JsonObject?> ProcessAsync(JsonNode? node, HttpContext context)
    {
        if (node is not JsonObject request)
            return ErrorResponse(null, -32600, "Invalid Request");

        var id = GetId(request, out var validId);
        if (!validId ||
            request["jsonrpc"] is not JsonValue version ||
            !version.TryGetValue<string>(out var versionText) ||
            versionText != "2.0" ||
            request["method"] is not JsonValue methodNode ||
            !methodNode.TryGetValue<string>(out var methodName) ||
            string.IsNullOrWhiteSpace(methodName))
            return ErrorResponse(null, -32600, "Invalid Request");

        var hasId = request.ContainsKey("id");
        var parametersNode = request["params"];
        if (request.ContainsKey("params") && parametersNode is not JsonObject)
            return hasId ? ErrorResponse(id, -32602, "Invalid params") : null;

        if (!methods.TryGetMethod(methodName, out var method))
            return hasId ? ErrorResponse(id, -32601, "Method not found") : null;

        if (method.RequiresAuthentication && context.User.Identity?.IsAuthenticated != true)
            return hasId ? ErrorResponse(id, -32001, "Authentication required") : null;

        if (method.RequiredRoles is { Length: > 0 } roles && !roles.Any(context.User.IsInRole))
            return hasId ? ErrorResponse(id, -32003, "Insufficient permissions") : null;

        try
        {
            var call = new JsonRpcCall(
                (JsonObject?)parametersNode ?? new JsonObject(),
                context.RequestServices,
                context.User,
                context.RequestAborted);
            var result = await method.Handler(call);
            return hasId ? ResultResponse(id, result) : null;
        }
        catch (JsonRpcInvalidParamsException exception)
        {
            return hasId ? ErrorResponse(id, -32602, exception.Message) : null;
        }
        catch (ArgumentException exception)
        {
            return hasId ? ErrorResponse(id, -32602, exception.Message) : null;
        }
        catch (KeyNotFoundException exception)
        {
            return hasId ? ErrorResponse(id, -32004, exception.Message) : null;
        }
        catch (UnauthorizedAccessException exception)
        {
            return hasId ? ErrorResponse(id, -32003, exception.Message) : null;
        }
        catch (InvalidOperationException exception)
        {
            return hasId ? ErrorResponse(id, -32000, exception.Message) : null;
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled error while executing JSON-RPC method {Method}", methodName);
            return hasId ? ErrorResponse(id, -32603, "Internal error") : null;
        }
    }

    private static async Task<JsonNode?> ParseBoundedAsync(Stream body, CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        while (true)
        {
            var read = await body.ReadAsync(chunk, cancellationToken);
            if (read == 0)
                break;
            if (buffer.Length + read > MaximumBodyBytes)
                throw new RequestTooLargeException();
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        buffer.Position = 0;
        return await JsonNode.ParseAsync(
            buffer,
            documentOptions: new JsonDocumentOptions { MaxDepth = 32 },
            cancellationToken: cancellationToken);
    }

    private static async Task WriteResponseAsync(HttpContext context, JsonNode response)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json; charset=utf-8";
        await JsonSerializer.SerializeAsync(
            context.Response.Body, response, SerializerOptions, context.RequestAborted);
    }

    private static JsonNode? GetId(JsonObject request, out bool valid)
    {
        valid = true;
        if (!request.TryGetPropertyValue("id", out var id) || id is null)
            return null;
        if (id is JsonValue value &&
            (value.TryGetValue<string>(out _) ||
             value.TryGetValue<long>(out _) ||
             value.TryGetValue<ulong>(out _)))
            return id.DeepClone();
        valid = false;
        return null;
    }

    private static JsonObject ResultResponse(JsonNode? id, object? result) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["result"] = JsonSerializer.SerializeToNode(result, SerializerOptions)
    };

    private static JsonObject ErrorResponse(JsonNode? id, int code, string message) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["error"] = new JsonObject { ["code"] = code, ["message"] = message }
    };

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class RequestTooLargeException : Exception;
}
