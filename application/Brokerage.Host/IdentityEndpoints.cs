using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Brokerage.Host.Services;
using Brokerage.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Brokerage.Host;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/identity").WithTags("Identity");

        group.MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .RequireRateLimiting("identity-auth")
            .WithName("RegisterIdentity")
            .WithSummary("Register a user with email and password")
            .Produces<IdentityAuthenticationResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting("identity-auth")
            .WithName("LoginIdentity")
            .WithSummary("Sign in with email and password")
            .Produces<IdentityAuthenticationResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        group.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .RequireRateLimiting("identity-auth")
            .WithName("RefreshIdentityTokens")
            .WithSummary("Rotate a refresh token and issue a new access token")
            .Produces<IdentityAuthenticationResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        group.MapPost("/logout", LogoutAsync)
            .AllowAnonymous()
            .RequireRateLimiting("identity-auth")
            .WithName("LogoutIdentity")
            .WithSummary("Revoke a refresh token")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();

        group.MapGet("/me", GetProfileAsync)
            .RequireAuthorization()
            .WithName("GetIdentityProfile")
            .WithSummary("Get the authenticated user's identity profile")
            .Produces<IdentityProfileResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterIdentityRequest request,
        IdentityService identityService,
        IdentityTokenService tokenService,
        CancellationToken cancellationToken)
    {
        try
        {
            var identity = await identityService.RegisterAsync(
                request.Email, request.Password, cancellationToken);
            return TypedResults.Created(
                "/api/identity/me",
                CreateAuthenticationResponse(identity, tokenService));
        }
        catch (InvalidOperationException exception)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Account already exists",
                detail: exception.Message);
        }
    }

    private static async Task<IResult> LoginAsync(
        LoginIdentityRequest request,
        IdentityService identityService,
        IdentityTokenService tokenService,
        CancellationToken cancellationToken)
    {
        try
        {
            var identity = await identityService.SignInAsync(
                request.Email, request.Password, cancellationToken);
            return TypedResults.Ok(CreateAuthenticationResponse(identity, tokenService));
        }
        catch (InvalidCredentialsException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid credentials",
                detail: "The email or password is invalid.");
        }
    }

    private static async Task<IResult> RefreshAsync(
        RefreshIdentityRequest request,
        IdentityService identityService,
        IdentityTokenService tokenService,
        CancellationToken cancellationToken)
    {
        try
        {
            var identity = await identityService.RotateRefreshTokenAsync(
                request.RefreshToken, cancellationToken);
            return TypedResults.Ok(CreateAuthenticationResponse(identity, tokenService));
        }
        catch (InvalidCredentialsException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid refresh token",
                detail: "The refresh token is invalid, expired, or revoked.");
        }
    }

    private static async Task<IResult> LogoutAsync(
        LogoutIdentityRequest request,
        IdentityService identityService,
        CancellationToken cancellationToken)
    {
        await identityService.RevokeRefreshTokenAsync(request.RefreshToken, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetProfileAsync(
        ClaimsPrincipal user,
        IdentityService identityService,
        CancellationToken cancellationToken)
    {
        var userIdText = user.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdText, out var userId))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid identity token");
        }

        var profile = await identityService.GetProfileAsync(userId, cancellationToken);
        return profile is null
            ? TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "User not found")
            : TypedResults.Ok(new IdentityProfileResponse(profile.UserId, profile.Email, profile.Roles));
    }

    private static IdentityAuthenticationResponse CreateAuthenticationResponse(
        AuthenticatedIdentity identity,
        IdentityTokenService tokenService)
    {
        var accessToken = tokenService.CreateAccessToken(identity);
        return new IdentityAuthenticationResponse(
            accessToken.Value,
            "Bearer",
            accessToken.ExpiresAt,
            identity.RefreshToken,
            identity.RefreshExpiresAt,
            new IdentityProfileResponse(identity.UserId, identity.Email, identity.Roles));
    }
}

/// <summary>Credentials used to create a new account.</summary>
public sealed record RegisterIdentityRequest
{
    /// <summary>Email address for the new account.</summary>
    [Required, EmailAddress, MaxLength(320)]
    public required string Email { get; init; }

    /// <summary>Password with at least 12 characters.</summary>
    [Required, MinLength(12), MaxLength(256)]
    public required string Password { get; init; }
}

/// <summary>Credentials used to sign in.</summary>
public sealed record LoginIdentityRequest
{
    /// <summary>Registered email address.</summary>
    [Required, EmailAddress, MaxLength(320)]
    public required string Email { get; init; }

    /// <summary>Account password.</summary>
    [Required, MinLength(1), MaxLength(256)]
    public required string Password { get; init; }
}

/// <summary>Refresh token used to rotate the current session.</summary>
public sealed record RefreshIdentityRequest
{
    /// <summary>Opaque refresh token returned by registration, login, or refresh.</summary>
    [Required, MinLength(32), MaxLength(128)]
    public required string RefreshToken { get; init; }
}

/// <summary>Refresh token whose session should be revoked.</summary>
public sealed record LogoutIdentityRequest
{
    /// <summary>Opaque refresh token returned by registration, login, or refresh.</summary>
    [Required, MinLength(32), MaxLength(128)]
    public required string RefreshToken { get; init; }
}

/// <summary>Authentication tokens and the associated user profile.</summary>
public sealed record IdentityAuthenticationResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    IdentityProfileResponse User);

/// <summary>Public profile information for an identity.</summary>
public sealed record IdentityProfileResponse(
    Guid UserId,
    string Email,
    IReadOnlyList<string> Roles);
