using System.Text.Json;
using System.Text.Json.Nodes;
using Brokerage.Common;
using Brokerage.Host.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Brokerage.Host;

public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/chat", HandleAsync)
            .RequireAuthorization()
            .WithName("SendChatMessage")
            .WithSummary("Send a message to the brokerage assistant")
            .WithDescription(
                "Sends a message and optional conversation history to the configured OpenAI-compatible chat provider. " +
                "The assistant can call authorized read-only module flows and prepare state-changing drafts.")
            .Produces<ChatMessageResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet("/api/chat/flows", GetFlows)
            .RequireAuthorization()
            .WithName("GetChatFlows")
            .WithSummary("List brokerage flows available to the assistant")
            .Produces<IReadOnlyList<ChatFlowResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        endpoints.MapPost("/api/chat/actions/{actionId:guid}/confirm", ConfirmActionAsync)
            .RequireAuthorization()
            .WithName("ConfirmChatAction")
            .WithSummary("Confirm and execute a brokerage action draft")
            .Produces<ChatActionExecutionResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        endpoints.MapGet("/api/chat/antiforgery", GetAntiforgeryToken)
            .RequireAuthorization()
            .WithName("GetChatAntiforgeryToken")
            .WithSummary("Get a request token for confirming chat action drafts")
            .Produces<ChatAntiforgeryResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        endpoints.MapDelete("/api/chat/actions/{actionId:guid}", CancelAction)
            .RequireAuthorization()
            .WithName("CancelChatAction")
            .WithSummary("Discard a brokerage action draft")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static Ok<IReadOnlyList<ChatFlowResponse>> GetFlows() =>
        TypedResults.Ok<IReadOnlyList<ChatFlowResponse>>(
            ChatFlowCatalog.Flows
                .Select(flow => new ChatFlowResponse(
                    flow.Method,
                    flow.IsReadOnly,
                    flow.Description,
                    flow.Parameters))
                .ToArray());

    private static async Task<Results<
        Ok<ChatMessageResponse>,
        BadRequest<ProblemDetails>,
        ProblemHttpResult>> HandleAsync(
        ChatMessageRequest request,
        HttpContext context,
        IChatService chatService,
        ILogger<OpenAiCompatibleChatService> logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 4000)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid chat message",
                Detail = "Message must contain 1 to 4,000 characters."
            });
        }

        if (request.History is null || request.History.Count > 20 ||
            request.History.Any(item =>
                item is null ||
                item.Role is not ("user" or "assistant") ||
                string.IsNullOrWhiteSpace(item.Content) ||
                item.Content.Length > 4000))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid chat history",
                Detail = "History may contain up to 20 user/assistant messages of at most 4,000 characters each."
            });
        }

        try
        {
            var response = await chatService.SendAsync(request, context.User, cancellationToken);
            return TypedResults.Ok(response);
        }
        catch (ChatServiceNotConfiguredException exception)
        {
            logger.LogWarning(exception, "Chat request rejected because the provider is not configured.");
            return TypedResults.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Chat service unavailable",
                detail: exception.Message);
        }
        catch (ChatProviderException exception)
        {
            logger.LogError(exception, "Chat provider request failed.");
            return TypedResults.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Chat provider error",
                detail: "The assistant could not complete this request. Please try again.");
        }
        catch (ArgumentException exception)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid chat history",
                Detail = exception.Message
            });
        }
    }

    private static async Task<IResult> ConfirmActionAsync(
        Guid actionId,
        HttpContext context,
        IAntiforgery antiforgery,
        ChatActionDraftStore draftStore,
        JsonRpcMethodCatalog methods,
        ILogger<OpenAiCompatibleChatService> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid confirmation request",
                detail: "Refresh the chat page and confirm the action again.");
        }

        if (!draftStore.TryTake(actionId, context.User, out var action))
            return TypedResults.NotFound();

        var flow = ChatFlowCatalog.Find(action.Method);
        if (flow is null || flow.IsReadOnly || !methods.TryGetMethod(action.Method, out var method))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Action is no longer available",
                detail: "Create a new draft and try again.");
        }

        try
        {
            OpenAiCompatibleChatService.EnsureMethodAccess(method, context.User);
            var parameters = JsonNode.Parse(action.Parameters.GetRawText()) as JsonObject;
            if (parameters is null)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Action draft is invalid",
                    detail: "Create a new draft and try again.");
            }

            var call = new JsonRpcCall(parameters, context.RequestServices, context.User, cancellationToken);
            var result = await method.Handler(call);
            var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return TypedResults.Ok(new ChatActionExecutionResponse(action.Method, resultJson));
        }
        catch (UnauthorizedAccessException exception)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Action is not authorized",
                detail: exception.Message);
        }
        catch (KeyNotFoundException exception)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Action could not be completed",
                detail: exception.Message);
        }
        catch (JsonRpcInvalidParamsException exception)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Action parameters are invalid",
                detail: exception.Message);
        }
        catch (ArgumentException exception)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Action parameters are invalid",
                detail: exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "Chat action {Method} could not be executed.", action.Method);
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Action could not be completed",
                detail: exception.Message);
        }
    }

    private static IResult CancelAction(
        Guid actionId,
        HttpContext context,
        ChatActionDraftStore draftStore) =>
        draftStore.TryCancel(actionId, context.User)
            ? TypedResults.NoContent()
            : TypedResults.NotFound();

    private static IResult GetAntiforgeryToken(HttpContext context, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        return TypedResults.Ok(new ChatAntiforgeryResponse(tokens.RequestToken!));
    }
}

public sealed record ChatAntiforgeryResponse(string Token);

/// <summary>Describes a workflow that the brokerage assistant can invoke.</summary>
public sealed record ChatFlowResponse(
    string Method,
    bool IsReadOnly,
    string Description,
    string Parameters);
