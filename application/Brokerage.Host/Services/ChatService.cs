using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Brokerage.Common;

namespace Brokerage.Host.Services;

public interface IChatService
{
    Task<ChatMessageResponse> SendAsync(
        ChatMessageRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken);
}

/// <summary>A message and optional prior turns for the brokerage assistant.</summary>
public sealed class ChatMessageRequest
{
    public required string Message { get; init; }
    public IReadOnlyList<ChatHistoryMessage> History { get; init; } = [];
}

/// <summary>A prior user or assistant turn in the conversation.</summary>
public sealed record ChatHistoryMessage(string Role, string Content);

/// <summary>The assistant's reply and any actions waiting for explicit user confirmation.</summary>
public sealed record ChatMessageResponse(
    string Reply,
    IReadOnlyList<ChatPendingAction> PendingActions);

public sealed class ChatServiceNotConfiguredException(string message) : Exception(message);

public sealed class ChatProviderException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class OpenAiCompatibleChatService(
    HttpClient httpClient,
    JsonRpcMethodCatalog methodCatalog,
    ChatActionDraftStore draftStore,
    IServiceScopeFactory scopeFactory,
    ChatProviderSettingsService providerSettingsService) : IChatService
{
    private const int MaximumToolCalls = 8;
    private const int MaximumToolRounds = 4;
    private const string SystemPrompt =
        "You are the Brokerage assistant. Answer clearly and concisely. " +
        "Use brokerage workflow tools for the registered module methods listed in their descriptions. " +
        "Read-only methods execute immediately under the authenticated user's permissions. " +
        "Every state-changing method only creates a draft; never claim it was executed until the user explicitly confirms it in the UI. " +
        "Do not invent brokerage data, permissions, or workflow results. " +
        "Market quotes are stored observations, not guarantees or investment advice.";

    public async Task<ChatMessageResponse> SendAsync(
        ChatMessageRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var (endpoint, model, apiKey) = await GetConfigurationAsync(cancellationToken);
        var messages = CreateMessages(request);
        var pendingActions = new List<ChatPendingAction>();
        var toolDefinitions = CreateToolDefinitions();
        var toolCallCount = 0;

        for (var round = 0; round < MaximumToolRounds; round++)
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(new
                {
                    model,
                    messages,
                    tools = toolDefinitions.DeepClone(),
                    tool_choice = "auto"
                })
            };
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrWhiteSpace(apiKey))
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            JsonDocument completion;
            try
            {
                using var response = await httpClient.SendAsync(
                    httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    throw new ChatProviderException(
                        $"The configured chat provider returned HTTP {(int)response.StatusCode}.");

                var responseBody = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                if (responseBody.Length > 1_048_576)
                    throw new ChatProviderException("The chat provider response exceeded the 1 MiB limit.");
                completion = JsonDocument.Parse(responseBody);
            }
            catch (ChatProviderException)
            {
                throw;
            }
            catch (HttpRequestException exception)
            {
                throw new ChatProviderException("Could not connect to the configured chat provider.", exception);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ChatProviderException("The configured chat provider timed out.", exception);
            }
            catch (JsonException exception)
            {
                throw new ChatProviderException("The configured chat provider returned invalid JSON.", exception);
            }

            using (completion)
            {
                var assistantMessage = GetAssistantMessage(completion.RootElement);
                if (!assistantMessage.TryGetProperty("tool_calls", out var toolCalls) ||
                    toolCalls.ValueKind != JsonValueKind.Array ||
                    toolCalls.GetArrayLength() == 0)
                {
                    if (assistantMessage.TryGetProperty("content", out var content) &&
                        content.ValueKind == JsonValueKind.String)
                    {
                        return new ChatMessageResponse(content.GetString()!, pendingActions);
                    }

                    throw new ChatProviderException("The configured chat provider returned no assistant message.");
                }

                var assistantNode = JsonNode.Parse(assistantMessage.GetRawText())
                    ?? throw new ChatProviderException("The chat provider returned an invalid tool-call message.");
                messages.Add(assistantNode);

                foreach (var toolCall in toolCalls.EnumerateArray())
                {
                    toolCallCount++;
                    if (toolCallCount > MaximumToolCalls)
                        throw new ChatProviderException("The assistant exceeded the permitted number of tool calls.");

                    var toolId = toolCall.TryGetProperty("id", out var idValue)
                        ? idValue.GetString()
                        : null;
                    var function = toolCall.TryGetProperty("function", out var functionValue)
                        ? functionValue
                        : default;
                    var name = function.ValueKind == JsonValueKind.Object &&
                               function.TryGetProperty("name", out var nameValue)
                        ? nameValue.GetString()
                        : null;
                    var arguments = function.ValueKind == JsonValueKind.Object &&
                                    function.TryGetProperty("arguments", out var argumentsValue)
                        ? argumentsValue.GetString()
                        : null;

                    if (string.IsNullOrWhiteSpace(toolId) || string.IsNullOrWhiteSpace(name))
                        throw new ChatProviderException("The chat provider returned an invalid tool call.");

                    var (result, pendingAction) = await ExecuteToolAsync(
                        name, arguments, user, cancellationToken);
                    if (pendingAction is not null)
                        pendingActions.Add(pendingAction);
                    messages.Add(new JsonObject
                    {
                        ["role"] = "tool",
                        ["tool_call_id"] = toolId,
                        ["content"] = result
                    });
                }
            }
        }

        throw new ChatProviderException("The assistant did not finish within the permitted tool-call rounds.");
    }

    private async Task<(Uri Endpoint, string Model, string? ApiKey)> GetConfigurationAsync(
        CancellationToken cancellationToken)
    {
        var configured = await providerSettingsService.GetAsync(cancellationToken);
        var endpointText = configured.Endpoint;
        var model = configured.Model;
        if (!Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("https" or "http") ||
            string.IsNullOrWhiteSpace(model))
        {
            throw new ChatServiceNotConfiguredException(
                "Chat is not configured. An administrator must set the provider endpoint and model in Settings.");
        }

        if (endpoint.Scheme != Uri.UriSchemeHttps &&
            !endpoint.IsLoopback)
        {
            throw new ChatServiceNotConfiguredException(
                "The chat provider endpoint must use HTTPS unless it is a loopback development endpoint.");
        }

        return (endpoint, model, configured.ApiKey);
    }

    private static JsonArray CreateMessages(ChatMessageRequest request)
    {
        var messages = new JsonArray
        {
            new JsonObject { ["role"] = "system", ["content"] = SystemPrompt }
        };

        foreach (var item in request.History)
        {
            if (item.Role is not ("user" or "assistant"))
                throw new ArgumentException("Chat history roles must be 'user' or 'assistant'.");

            messages.Add(new JsonObject
            {
                ["role"] = item.Role,
                ["content"] = item.Content
            });
        }

        messages.Add(new JsonObject { ["role"] = "user", ["content"] = request.Message });
        return messages;
    }

    private static JsonElement GetAssistantMessage(JsonElement root)
    {
        if (root.TryGetProperty("choices", out var choices) &&
            choices.ValueKind == JsonValueKind.Array &&
            choices.GetArrayLength() > 0 &&
            choices[0].TryGetProperty("message", out var message) &&
            message.ValueKind == JsonValueKind.Object)
            return message;

        throw new ChatProviderException("The configured chat provider response is missing an assistant message.");
    }

    private async Task<(string Result, ChatPendingAction? PendingAction)> ExecuteToolAsync(
        string? name,
        string? arguments,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (arguments is null)
            return ("Tool arguments were missing.", null);

        try
        {
            using var document = JsonDocument.Parse(arguments);
            var root = document.RootElement;
            if (name != "brokerage_module_flow" ||
                root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("method", out var methodNode) ||
                methodNode.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("parameters", out var parametersNode) ||
                parametersNode.ValueKind != JsonValueKind.Object)
                return ("Invalid brokerage workflow arguments.", null);

            var methodName = methodNode.GetString()!;
            var flow = ChatFlowCatalog.Find(methodName);
            if (flow is null || !methodCatalog.TryGetMethod(methodName, out var method))
                return ("The requested brokerage workflow is not available.", null);

            EnsureMethodAccess(method, user);
            var parameters = JsonNode.Parse(parametersNode.GetRawText()) as JsonObject;
            if (parameters is null)
                return ("Workflow parameters must be a JSON object.", null);

            if (!flow.IsReadOnly)
            {
                var missingParameters = GetRequiredParameters(flow)
                    .Where(parameter =>
                        !parameters.TryGetPropertyValue(parameter, out var value) || value is null)
                    .ToArray();
                if (missingParameters.Length > 0)
                    return ($"Missing required workflow parameters: {string.Join(", ", missingParameters)}.", null);

                var draft = draftStore.Create(user, methodName, flow.Description, parameters);
                return (
                    JsonSerializer.Serialize(new
                    {
                        status = "pending_confirmation",
                        action = draft
                    }),
                    draft);
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var call = new JsonRpcCall(parameters, scope.ServiceProvider, user, cancellationToken);
            var result = await method.Handler(call);
            return (JsonSerializer.Serialize(new { status = "completed", result }), null);
        }
        catch (UnauthorizedAccessException)
        {
            return ("Access denied for this brokerage workflow.", null);
        }
        catch (KeyNotFoundException)
        {
            return ("The requested brokerage record was not found.", null);
        }
        catch (JsonRpcInvalidParamsException exception)
        {
            return ($"Invalid workflow parameters: {exception.Message}", null);
        }
        catch (ArgumentException exception)
        {
            return ($"Invalid workflow parameters: {exception.Message}", null);
        }
        catch (InvalidOperationException exception)
        {
            return ($"The workflow could not be completed: {exception.Message}", null);
        }
        catch (JsonException)
        {
            return ("Tool arguments were not valid JSON.", null);
        }
    }

    private JsonArray CreateToolDefinitions()
    {
        var flows = ChatFlowCatalog.Flows
            .Where(flow => methodCatalog.TryGetMethod(flow.Method, out _))
            .ToArray();
        var flowDescriptions = string.Join(
            "\n",
            flows.Select(flow =>
                $"- {flow.Method} ({(flow.IsReadOnly ? "read-only" : "draft, confirmation required")}): " +
                $"{flow.Description} Parameters: {flow.Parameters}."));
        return
        [
            JsonSerializer.SerializeToNode(new
            {
                type = "function",
                function = new
                {
                    name = "brokerage_module_flow",
                    description =
                        "Run one registered brokerage module flow. Read-only methods run immediately; " +
                        "mutating methods only create a draft for explicit user confirmation. Available flows:\n" +
                        flowDescriptions,
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            method = new
                            {
                                type = "string",
                                @enum = flows.Select(flow => flow.Method).ToArray(),
                                description = "The exact registered brokerage module method."
                            },
                            parameters = new
                            {
                                type = "object",
                                description = "Arguments for the selected method, using the parameter names and types listed in its description."
                            }
                        },
                        required = new[] { "method", "parameters" },
                        additionalProperties = false
                    }
                }
            })
        ];
    }

    internal static void EnsureMethodAccess(JsonRpcMethod method, ClaimsPrincipal user)
    {
        if (method.RequiresAuthentication && user.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("Authentication is required.");

        if (method.RequiredRoles is { Length: > 0 } roles && !roles.Any(user.IsInRole))
            throw new UnauthorizedAccessException("Insufficient permissions for this brokerage workflow.");
    }

    private static IEnumerable<string> GetRequiredParameters(ChatFlow flow)
    {
        var requiredPart = flow.Parameters.Split(", optional ", 2, StringSplitOptions.None)[0];
        if (requiredPart == "no parameters")
            return [];

        return requiredPart
            .Split(", ", StringSplitOptions.TrimEntries)
            .Select(parameter => parameter.Split(' ', 2)[0]);
    }
}
