using Microsoft.EntityFrameworkCore;

namespace Brokerage.Documents;

public sealed class DocumentsService(DocumentsDb db)
{
    public async Task<StoredDocument> RegisterDocumentAsync(
        Guid? clientId, string documentType, string storageKey, string contentType,
        long sizeBytes, string? sha256, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        if (sizeBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        ValidateHash(sha256);
        var document = new StoredDocument
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            DocumentType = documentType.Trim(),
            CurrentStorageKey = storageKey.Trim(),
            ContentType = contentType.Trim(),
            SizeBytes = sizeBytes,
            Sha256 = sha256?.ToLowerInvariant(),
            Status = DocumentStatus.Pending
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync(cancellationToken);
        return document;
    }

    public async Task<DocumentVersion> AddVersionAsync(
        Guid documentId, string storageKey, string contentType, long sizeBytes,
        string? sha256, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        if (sizeBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        ValidateHash(sha256);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var document = await db.Documents.Include(x => x.Versions)
            .SingleOrDefaultAsync(x => x.Id == documentId, cancellationToken)
            ?? throw new KeyNotFoundException("Document was not found.");
        if (document.Status == DocumentStatus.Deleted)
            throw new InvalidOperationException("Deleted documents cannot be versioned.");
        var version = new DocumentVersion
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            VersionNumber = document.Versions.Count == 0 ? 1 : document.Versions.Max(x => x.VersionNumber) + 1,
            StorageKey = storageKey.Trim(),
            ContentType = contentType.Trim(),
            SizeBytes = sizeBytes,
            Sha256 = sha256?.ToLowerInvariant()
        };
        document.CurrentStorageKey = version.StorageKey;
        document.ContentType = version.ContentType;
        document.SizeBytes = version.SizeBytes;
        document.Sha256 = version.Sha256;
        document.Status = DocumentStatus.Pending;
        db.Versions.Add(version);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return version;
    }

    public async Task SetScanResultAsync(
        Guid documentId, bool clean, CancellationToken cancellationToken = default)
    {
        var document = await db.Documents.SingleOrDefaultAsync(x => x.Id == documentId, cancellationToken)
            ?? throw new KeyNotFoundException("Document was not found.");
        if (document.Status != DocumentStatus.Pending)
            throw new InvalidOperationException("Only pending documents can receive a scan result.");
        document.Status = clean ? DocumentStatus.Available : DocumentStatus.Quarantined;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<RetentionPolicy> SetRetentionPolicyAsync(
        string documentType, int retentionDays, bool legalHoldByDefault,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentType);
        if (retentionDays < 1)
            throw new ArgumentOutOfRangeException(nameof(retentionDays));
        var policy = await db.RetentionPolicies.SingleOrDefaultAsync(x => x.DocumentType == documentType, cancellationToken);
        if (policy is null)
        {
            policy = new RetentionPolicy
            {
                Id = Guid.NewGuid(),
                DocumentType = documentType.Trim(),
                RetentionDays = retentionDays,
                LegalHoldByDefault = legalHoldByDefault
            };
            db.RetentionPolicies.Add(policy);
        }
        else
        {
            policy.RetentionDays = retentionDays;
            policy.LegalHoldByDefault = legalHoldByDefault;
            policy.IsActive = true;
        }
        await db.SaveChangesAsync(cancellationToken);
        return policy;
    }

    private static void ValidateHash(string? sha256)
    {
        if (sha256 is not null && (sha256.Length != 64 || !sha256.All(Uri.IsHexDigit)))
            throw new ArgumentException("SHA-256 must be a 64-character hexadecimal string.", nameof(sha256));
    }
}
