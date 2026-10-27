using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Brokerage.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Brokerage.Host.Services;

public sealed class IdentityTokenOptions
{
    private IdentityTokenOptions(string issuer, string audience, string signingKey)
    {
        Issuer = issuer;
        Audience = audience;
        SigningCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
    }

    public string Issuer { get; }
    public string Audience { get; }
    public SigningCredentials SigningCredentials { get; }
    public SymmetricSecurityKey SigningKey => (SymmetricSecurityKey)SigningCredentials.Key;

    public static IdentityTokenOptions FromConfiguration(IConfiguration configuration)
    {
        var signingKey = configuration["Identity:JwtSigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException(
                "Identity:JwtSigningKey must be configured with at least 32 bytes of secret material.");
        }

        return new IdentityTokenOptions(
            configuration["Identity:Issuer"] ?? "Brokerage.Host",
            configuration["Identity:Audience"] ?? "Brokerage.Api",
            signingKey);
    }
}

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public sealed class IdentityTokenService(
    IdentityTokenOptions options,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    public AccessToken CreateAccessToken(AuthenticatedIdentity identity)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.Add(AccessTokenLifetime);
        var claims = new List<Claim>
        {
            new("sub", identity.UserId.ToString()),
            new("email", identity.Email),
            new("sid", identity.SessionId.ToString())
        };
        claims.AddRange(identity.Roles.Select(role => new Claim("role", role)));

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: options.SigningCredentials);

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
