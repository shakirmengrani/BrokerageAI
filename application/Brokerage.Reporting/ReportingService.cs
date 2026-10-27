using Microsoft.EntityFrameworkCore;

namespace Brokerage.Reporting;

public sealed class ReportingService(ReportingDb db)
{
    public async Task<AuditEvent> RecordAuditEventAsync(
        string actorId, string action, string entityType, string? entityId,
        string? correlationId, string? payloadJson, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        var item = new AuditEvent
        {
            Id = Guid.NewGuid(),
            ActorId = actorId.Trim(),
            Action = action.Trim(),
            EntityType = entityType.Trim(),
            EntityId = entityId,
            CorrelationId = correlationId,
            PayloadJson = payloadJson
        };
        db.AuditEvents.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task<ReportJob> QueueReportAsync(
        string reportType, string requestedBy, string? parametersJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportType);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedBy);
        var job = new ReportJob
        {
            Id = Guid.NewGuid(),
            ReportType = reportType.Trim(),
            RequestedBy = requestedBy.Trim(),
            ParametersJson = parametersJson
        };
        db.ReportJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task<ReportArtifact> CompleteReportAsync(
        Guid jobId, string storageKey, string contentType, long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        if (sizeBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var job = await db.ReportJobs.SingleOrDefaultAsync(x => x.Id == jobId, cancellationToken)
            ?? throw new KeyNotFoundException("Report job was not found.");
        if (job.Status is not (ReportJobStatus.Queued or ReportJobStatus.Running))
            throw new InvalidOperationException("Only queued or running report jobs can be completed.");
        var artifact = new ReportArtifact
        {
            Id = Guid.NewGuid(),
            ReportJobId = jobId,
            StorageKey = storageKey.Trim(),
            ContentType = contentType.Trim(),
            SizeBytes = sizeBytes
        };
        db.ReportArtifacts.Add(artifact);
        job.Status = ReportJobStatus.Completed;
        job.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return artifact;
    }

    public async Task MarkReportFailedAsync(
        Guid jobId, string errorCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        var job = await db.ReportJobs.SingleOrDefaultAsync(x => x.Id == jobId, cancellationToken)
            ?? throw new KeyNotFoundException("Report job was not found.");
        if (job.Status is ReportJobStatus.Completed or ReportJobStatus.Cancelled)
            throw new InvalidOperationException("Completed or cancelled reports cannot be marked failed.");
        job.Status = ReportJobStatus.Failed;
        job.ErrorCode = errorCode.Trim();
        job.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
