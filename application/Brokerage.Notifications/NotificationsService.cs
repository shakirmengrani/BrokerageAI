using Microsoft.EntityFrameworkCore;

namespace Brokerage.Notifications;

public sealed class NotificationsService(NotificationsDb db)
{
    public async Task<Notification> EnqueueAsync(
        Guid? clientId, string channel, string destination, string template,
        string? payloadJson, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Channel = channel.Trim(),
            Destination = destination.Trim(),
            Template = template.Trim(),
            PayloadJson = payloadJson
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(cancellationToken);
        return notification;
    }

    public async Task<DeliveryAttempt> RecordDeliveryAttemptAsync(
        Guid notificationId, bool succeeded, string? providerReference, string? errorCode,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var notification = await db.Notifications.SingleOrDefaultAsync(x => x.Id == notificationId, cancellationToken)
            ?? throw new KeyNotFoundException("Notification was not found.");
        if (notification.Status is NotificationStatus.Sent or NotificationStatus.Cancelled)
            throw new InvalidOperationException("Sent or cancelled notifications cannot be retried.");
        var attempt = new DeliveryAttempt
        {
            Id = Guid.NewGuid(),
            NotificationId = notificationId,
            Status = succeeded ? DeliveryAttemptStatus.Succeeded : DeliveryAttemptStatus.Failed,
            ProviderReference = providerReference,
            ErrorCode = errorCode
        };
        db.DeliveryAttempts.Add(attempt);
        notification.Status = succeeded ? NotificationStatus.Sent : NotificationStatus.Failed;
        notification.SentAt = succeeded ? DateTimeOffset.UtcNow : null;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return attempt;
    }

    public async Task<NotificationPreference> SetPreferenceAsync(
        Guid clientId, string channel, string category, bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        var preference = await db.Preferences.SingleOrDefaultAsync(
            x => x.ClientId == clientId && x.Channel == channel && x.Category == category, cancellationToken);
        if (preference is null)
        {
            preference = new NotificationPreference
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                Channel = channel.Trim(),
                Category = category.Trim(),
                IsEnabled = isEnabled
            };
            db.Preferences.Add(preference);
        }
        else
        {
            preference.IsEnabled = isEnabled;
            preference.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        return preference;
    }

    public Task<List<Notification>> GetQueuedAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 1000.");
        return db.Notifications.AsNoTracking().Where(x => x.Status == NotificationStatus.Queued)
            .OrderBy(x => x.CreatedAt).Take(limit).ToListAsync(cancellationToken);
    }
}
