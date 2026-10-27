namespace Brokerage.Clients;

public enum ClientStatus
{
    Pending,
    Active,
    Restricted,
    Closed
}

public enum KycStatus
{
    Pending,
    InReview,
    Approved,
    Rejected
}

public sealed class Client
{
    public Guid Id { get; init; }
    public required string ExternalReference { get; set; }
    public required string GivenName { get; set; }
    public required string FamilyName { get; set; }
    public required string Email { get; set; }
    public ClientStatus Status { get; set; } = ClientStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public ICollection<ClientContact> Contacts { get; } = [];
    public ICollection<ClientDocument> Documents { get; } = [];
    public ICollection<KycRecord> KycRecords { get; } = [];
}

public sealed class ClientContact
{
    public Guid Id { get; init; }
    public Guid ClientId { get; init; }
    public required string Type { get; set; }
    public required string Value { get; set; }
    public bool IsPrimary { get; set; }
    public Client Client { get; init; } = null!;
}

public sealed class ClientDocument
{
    public Guid Id { get; init; }
    public Guid ClientId { get; init; }
    public required string DocumentType { get; set; }
    public required string StorageKey { get; set; }
    public DateTimeOffset UploadedAt { get; init; } = DateTimeOffset.UtcNow;
    public Client Client { get; init; } = null!;
}

public sealed class KycRecord
{
    public Guid Id { get; init; }
    public Guid ClientId { get; init; }
    public KycStatus Status { get; set; } = KycStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public Client Client { get; init; } = null!;
}
