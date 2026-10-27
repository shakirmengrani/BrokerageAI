using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Integrations;

public sealed class IntegrationsService(IntegrationsDb db)
{
    public async Task<IntegrationEndpoint> RegisterEndpointAsync(
        string name, string provider, string baseAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseAddress);
        if (!Uri.TryCreate(baseAddress, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Integration endpoint must be an absolute HTTPS URL.", nameof(baseAddress));
        var endpoint = new IntegrationEndpoint
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Provider = provider.Trim(),
            BaseAddress = uri.AbsoluteUri
        };
        db.Endpoints.Add(endpoint);
        await db.SaveChangesAsync(cancellationToken);
        return endpoint;
    }

    public async Task SetCredentialReferenceAsync(
        Guid endpointId, string secretReference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretReference);
        var endpoint = await db.Endpoints.Include(x => x.CredentialReference)
            .SingleOrDefaultAsync(x => x.Id == endpointId, cancellationToken)
            ?? throw new KeyNotFoundException("Integration endpoint was not found.");
        if (endpoint.CredentialReference is null)
        {
            endpoint.CredentialReference = new IntegrationCredentialReference
            {
                Id = Guid.NewGuid(),
                EndpointId = endpointId,
                SecretReference = secretReference.Trim()
            };
        }
        else
        {
            endpoint.CredentialReference.SecretReference = secretReference.Trim();
            endpoint.CredentialReference.RotatedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IntegrationMessage> EnqueueMessageAsync(
        Guid endpointId, string messageReference, string messageType,
        IntegrationMessageDirection direction, string? payloadJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        if (payloadJson is not null)
        {
            using var payload = JsonDocument.Parse(payloadJson);
        }
        if (!await db.Endpoints.AnyAsync(x => x.Id == endpointId && x.IsActive, cancellationToken))
            throw new KeyNotFoundException("Active integration endpoint was not found.");
        if (await db.Messages.AnyAsync(
                x => x.EndpointId == endpointId && x.MessageReference == messageReference, cancellationToken))
            throw new InvalidOperationException("Message reference already exists for this endpoint.");
        var message = new IntegrationMessage
        {
            Id = Guid.NewGuid(),
            EndpointId = endpointId,
            MessageReference = messageReference.Trim(),
            MessageType = messageType.Trim(),
            Direction = direction,
            PayloadJson = payloadJson
        };
        db.Messages.Add(message);
        await db.SaveChangesAsync(cancellationToken);
        return message;
    }

    public async Task UpdateMessageStatusAsync(
        Guid messageId, IntegrationMessageStatus status, CancellationToken cancellationToken = default)
    {
        var message = await db.Messages.SingleOrDefaultAsync(x => x.Id == messageId, cancellationToken)
            ?? throw new KeyNotFoundException("Integration message was not found.");
        var allowed = message.Status switch
        {
            IntegrationMessageStatus.Pending => status == IntegrationMessageStatus.Processing,
            IntegrationMessageStatus.Processing => status is IntegrationMessageStatus.Succeeded
                or IntegrationMessageStatus.Failed or IntegrationMessageStatus.DeadLettered,
            IntegrationMessageStatus.Failed => status is IntegrationMessageStatus.Pending or IntegrationMessageStatus.DeadLettered,
            _ => false
        };
        if (!allowed)
            throw new InvalidOperationException($"Integration message cannot transition from {message.Status} to {status}.");
        message.Status = status;
        if (status is IntegrationMessageStatus.Processing or IntegrationMessageStatus.Pending)
            message.AttemptCount++;
        if (status is IntegrationMessageStatus.Succeeded or IntegrationMessageStatus.DeadLettered)
            message.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
