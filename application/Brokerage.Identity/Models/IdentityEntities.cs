namespace Brokerage.Identity;

public sealed class User
{
    public Guid Id { get; init; }
    public required string Email { get; set; }
    public required string NormalizedEmail { get; set; }
    public required string PasswordHash { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public ICollection<UserRole> UserRoles { get; } = [];
    public ICollection<Session> Sessions { get; } = [];
}

public sealed class Role
{
    public Guid Id { get; init; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public ICollection<UserRole> UserRoles { get; } = [];
    public ICollection<RolePermission> RolePermissions { get; } = [];
}

public sealed class Permission
{
    public Guid Id { get; init; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public ICollection<RolePermission> RolePermissions { get; } = [];
}

public sealed class UserRole
{
    public Guid UserId { get; init; }
    public Guid RoleId { get; init; }
    public User User { get; init; } = null!;
    public Role Role { get; init; } = null!;
}

public sealed class RolePermission
{
    public Guid RoleId { get; init; }
    public Guid PermissionId { get; init; }
    public Role Role { get; init; } = null!;
    public Permission Permission { get; init; } = null!;
}

public sealed class Session
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public required string TokenHash { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public User User { get; init; } = null!;
}

public sealed class ChatProviderSettings
{
    public int Id { get; init; }
    public required string Endpoint { get; set; }
    public required string Model { get; set; }
    public string? EncryptedApiKey { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid UpdatedByUserId { get; set; }
}
