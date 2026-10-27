using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Brokerage.Host.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Brokerage.Host;

public static class ChatSettingsEndpoints
{
    public static IEndpointRouteBuilder MapChatSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/settings/chat")
            .WithTags("Administration")
            .RequireAuthorization(policy => policy.RequireRole("Administrator"));

        group.MapGet("/", GetAsync)
            .WithName("GetChatProviderSettings")
            .WithSummary("Get chat provider settings without revealing the API key")
            .Produces<ChatProviderSettingsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPut("/", SaveAsync)
            .WithName("SaveChatProviderSettings")
            .WithSummary("Update the active chat provider settings")
            .Produces<ChatProviderSettingsResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    private static async Task<Ok<ChatProviderSettingsResponse>> GetAsync(
        ChatProviderSettingsService settingsService,
        CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        return TypedResults.Ok(new ChatProviderSettingsResponse(
            settings.Endpoint,
            settings.Model,
            settings.HasApiKey));
    }

    private static async Task<Results<Ok<ChatProviderSettingsResponse>, BadRequest<ProblemDetails>>> SaveAsync(
        UpdateChatProviderSettingsRequest request,
        ClaimsPrincipal principal,
        ChatProviderSettingsService settingsService,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue("sub"), out var userId))
            return TypedResults.BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid administrator identity"
            });

        try
        {
            await settingsService.SaveAsync(
                request.Endpoint,
                request.Model,
                request.ApiKey,
                userId,
                cancellationToken);
            var settings = await settingsService.GetAsync(cancellationToken);
            return TypedResults.Ok(new ChatProviderSettingsResponse(
                settings.Endpoint,
                settings.Model,
                settings.HasApiKey));
        }
        catch (ArgumentException exception)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid chat provider settings",
                Detail = exception.Message
            });
        }
    }
}

/// <summary>Chat provider settings returned to an administrator; the API key is never included.</summary>
public sealed record ChatProviderSettingsResponse(
    string? Endpoint,
    string? Model,
    bool HasApiKey);

/// <summary>Updates the active OpenAI-compatible chat provider.</summary>
public sealed record UpdateChatProviderSettingsRequest
{
    /// <summary>Full chat completions URL.</summary>
    [Required, Url, MaxLength(2048)]
    public required string Endpoint { get; init; }

    /// <summary>Model identifier accepted by the provider.</summary>
    [Required, MinLength(1), MaxLength(200)]
    public required string Model { get; init; }

    /// <summary>Optional replacement API key; leave empty to keep the current key.</summary>
    [MaxLength(4096)]
    public string? ApiKey { get; init; }
}
