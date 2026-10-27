using System.Security.Cryptography;
using System.Text;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Brokerage.Identity;

public sealed record AuthenticatedIdentity(
    Guid UserId,
    string Email,
    IReadOnlyList<string> Roles,
    Guid SessionId,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAt);

public sealed class IdentityService(
    IdentityDb db,
    IPasswordHasher<User> passwordHasher,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
    private const long BootstrapAdministratorLockId = 714285391;

    public async Task EnsureBootstrapAdministratorAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ValidatePassword(password);
        var normalizedEmail = NormalizeEmail(email);
        if (!new EmailAddressAttribute().IsValid(email.Trim()))
            throw new ArgumentException("A valid email address is required.", nameof(email));

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            $"SELECT pg_advisory_xact_lock({BootstrapAdministratorLockId})",
            cancellationToken);

        var hasAdministrator = await db.UserRoles.AnyAsync(
            assignment => assignment.Role.NormalizedName == "ADMINISTRATOR" &&
                          assignment.User.IsActive,
            cancellationToken);
        if (hasAdministrator)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var user = await db.Users.SingleOrDefaultAsync(
            candidate => candidate.NormalizedEmail == normalizedEmail,
            cancellationToken);
        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = email.Trim(),
                NormalizedEmail = normalizedEmail,
                PasswordHash = string.Empty
            };
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            db.Users.Add(user);
        }
        else
        {
            if (!user.IsActive)
                throw new InvalidOperationException("The configured bootstrap account is inactive.");

            var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (verification == PasswordVerificationResult.Failed)
            {
                throw new InvalidOperationException(
                    "The configured bootstrap email already belongs to an account with a different password.");
            }

            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
                user.PasswordHash = passwordHasher.HashPassword(user, password);
        }

        var administratorRole = await db.Roles.SingleOrDefaultAsync(
            role => role.NormalizedName == "ADMINISTRATOR",
            cancellationToken);
        if (administratorRole is null)
        {
            administratorRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = "Administrator",
                NormalizedName = "ADMINISTRATOR"
            };
            db.Roles.Add(administratorRole);
        }

        if (!await db.UserRoles.AnyAsync(
                assignment => assignment.UserId == user.Id &&
                              assignment.RoleId == administratorRole.Id,
                cancellationToken))
        {
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = administratorRole.Id });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<AuthenticatedIdentity> RegisterAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ValidatePassword(password);
        var normalizedEmail = NormalizeEmail(email);
        if (await db.Users.AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken))
            throw new InvalidOperationException("A user with this email already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = string.Empty
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.GetBaseException() is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_users_NormalizedEmail"
            })
        {
            throw new InvalidOperationException("A user with this email already exists.", exception);
        }

        var identity = await CreateAuthenticatedSessionAsync(user, [], cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return identity;
    }

    public async Task<AuthenticatedIdentity> SignInAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var user = await db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(
                x => x.NormalizedEmail == NormalizeEmail(email) && x.IsActive,
                cancellationToken);
        if (user is null)
        {
            _ = passwordHasher.HashPassword(new User
            {
                Id = Guid.Empty,
                Email = "invalid@example.invalid",
                NormalizedEmail = "INVALID@EXAMPLE.INVALID",
                PasswordHash = string.Empty
            }, password);
            throw new InvalidCredentialsException();
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
            throw new InvalidCredentialsException();
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = passwordHasher.HashPassword(user, password);

        return await CreateAuthenticatedSessionAsync(
            user,
            user.UserRoles.Select(x => x.Role.Name).Order(StringComparer.Ordinal).ToArray(),
            cancellationToken);
    }

    public async Task<AuthenticatedIdentity> RotateRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        var now = timeProvider.GetUtcNow();
        var tokenHash = HashToken(refreshToken);
        var session = await db.Sessions
            .Include(x => x.User)
            .ThenInclude(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(
                x => x.TokenHash == tokenHash &&
                     x.RevokedAt == null &&
                     x.ExpiresAt > now &&
                     x.User.IsActive,
                cancellationToken);
        if (session is null)
            throw new InvalidCredentialsException();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var revoked = await db.Sessions
            .Where(x => x.Id == session.Id && x.RevokedAt == null && x.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.RevokedAt, now),
                cancellationToken);
        if (revoked != 1)
            throw new InvalidCredentialsException();

        var identity = await CreateAuthenticatedSessionAsync(
            session.User,
            session.User.UserRoles.Select(x => x.Role.Name).Order(StringComparer.Ordinal).ToArray(),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return identity;
    }

    public async Task<bool> RevokeRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return false;

        var now = timeProvider.GetUtcNow();
        var tokenHash = HashToken(refreshToken);
        var revoked = await db.Sessions
            .Where(x => x.TokenHash == tokenHash && x.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.RevokedAt, now),
                cancellationToken);
        return revoked > 0;
    }

    public async Task<IdentityProfile?> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users
            .AsNoTracking()
            .Where(x => x.Id == userId && x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.Email,
                Roles = x.UserRoles.Select(userRole => userRole.Role.Name)
            })
            .SingleOrDefaultAsync(cancellationToken);
        return user is null
            ? null
            : new IdentityProfile(
                user.Id,
                user.Email,
                user.Roles.Order(StringComparer.Ordinal).ToArray());
    }

    public async Task<Role> CreateRoleAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalizedName = name.Trim().ToUpperInvariant();
        if (await db.Roles.AnyAsync(x => x.NormalizedName == normalizedName, cancellationToken))
            throw new InvalidOperationException("A role with this name already exists.");

        var role = new Role { Id = Guid.NewGuid(), Name = name.Trim(), NormalizedName = normalizedName };
        db.Roles.Add(role);
        await db.SaveChangesAsync(cancellationToken);
        return role;
    }

    public async Task AssignRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        if (!await db.Users.AnyAsync(x => x.Id == userId, cancellationToken))
            throw new KeyNotFoundException("User was not found.");
        if (!await db.Roles.AnyAsync(x => x.Id == roleId, cancellationToken))
            throw new KeyNotFoundException("Role was not found.");
        if (await db.UserRoles.AnyAsync(x => x.UserId == userId && x.RoleId == roleId, cancellationToken))
            return;

        db.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthenticatedIdentity> CreateAuthenticatedSessionAsync(
        User user,
        IReadOnlyList<string> roles,
        CancellationToken cancellationToken)
    {
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var expiresAt = timeProvider.GetUtcNow().Add(RefreshTokenLifetime);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(refreshToken),
            ExpiresAt = expiresAt
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return new AuthenticatedIdentity(user.Id, user.Email, roles, session.Id, refreshToken, expiresAt);
    }

    private static string NormalizeEmail(string email) =>
        email.Trim().ToUpperInvariant();

    private static void ValidatePassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        if (password.Length is < 12 or > 256)
            throw new ArgumentException("Password must contain 12 to 256 characters.", nameof(password));
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public sealed record IdentityProfile(Guid UserId, string Email, IReadOnlyList<string> Roles);

public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("The email or password is invalid.")
    {
    }
}
