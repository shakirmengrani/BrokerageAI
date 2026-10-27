using Microsoft.EntityFrameworkCore;

namespace Brokerage.Clients;

public sealed class ClientsService(ClientsDb db)
{
    public async Task<Client> RegisterClientAsync(
        string externalReference, string givenName, string familyName, string email,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(givenName);
        ArgumentException.ThrowIfNullOrWhiteSpace(familyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        if (await db.Clients.AnyAsync(x => x.ExternalReference == externalReference, cancellationToken))
            throw new InvalidOperationException("A client with this external reference already exists.");

        var client = new Client
        {
            Id = Guid.NewGuid(),
            ExternalReference = externalReference.Trim(),
            GivenName = givenName.Trim(),
            FamilyName = familyName.Trim(),
            Email = email.Trim(),
            Status = ClientStatus.Pending
        };
        db.Clients.Add(client);
        await db.SaveChangesAsync(cancellationToken);
        return client;
    }

    public async Task<KycRecord> StartKycAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        if (!await db.Clients.AnyAsync(x => x.Id == clientId, cancellationToken))
            throw new KeyNotFoundException("Client was not found.");

        var record = new KycRecord { Id = Guid.NewGuid(), ClientId = clientId, Status = KycStatus.Pending };
        db.KycRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        return record;
    }

    public async Task ReviewKycAsync(
        Guid recordId, KycStatus decision, string reviewerId, CancellationToken cancellationToken = default)
    {
        if (decision is not (KycStatus.Approved or KycStatus.Rejected))
            throw new ArgumentOutOfRangeException(nameof(decision), "A KYC review must approve or reject.");
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewerId);
        var record = await db.KycRecords.SingleOrDefaultAsync(x => x.Id == recordId, cancellationToken)
            ?? throw new KeyNotFoundException("KYC record was not found.");
        if (record.Status is not (KycStatus.Pending or KycStatus.InReview))
            throw new InvalidOperationException("Only pending or in-review KYC records can be reviewed.");

        record.Status = decision;
        record.ReviewedBy = reviewerId;
        record.ReviewedAt = DateTimeOffset.UtcNow;
        if (decision == KycStatus.Approved)
        {
            var client = await db.Clients.SingleAsync(x => x.Id == record.ClientId, cancellationToken);
            client.Status = ClientStatus.Active;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<Client?> GetClientAsync(Guid clientId, CancellationToken cancellationToken = default) =>
        db.Clients.AsNoTracking().SingleOrDefaultAsync(x => x.Id == clientId, cancellationToken);
}
