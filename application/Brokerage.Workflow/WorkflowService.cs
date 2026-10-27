using Microsoft.EntityFrameworkCore;

namespace Brokerage.Workflow;

public sealed class WorkflowService(WorkflowDb db)
{
    public async Task<WorkflowDefinition> CreateDefinitionAsync(
        string name, string version, string? definitionJson, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        var definition = new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Version = version.Trim(),
            DefinitionJson = definitionJson
        };
        db.Definitions.Add(definition);
        await db.SaveChangesAsync(cancellationToken);
        return definition;
    }

    public async Task<WorkflowInstance> StartWorkflowAsync(
        Guid definitionId, string subjectType, string subjectId, string submittedBy,
        IReadOnlyCollection<(string TaskType, string AssignedRole)> tasks,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectType);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(submittedBy);
        ArgumentNullException.ThrowIfNull(tasks);
        if (tasks.Count == 0 || tasks.Any(x => string.IsNullOrWhiteSpace(x.TaskType) || string.IsNullOrWhiteSpace(x.AssignedRole)))
            throw new ArgumentException("A workflow requires at least one task with a task type and role.", nameof(tasks));
        var definition = await db.Definitions.SingleOrDefaultAsync(x => x.Id == definitionId && x.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("Active workflow definition was not found.");
        var instance = new WorkflowInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = definition.Id,
            SubjectType = subjectType.Trim(),
            SubjectId = subjectId.Trim(),
            SubmittedBy = submittedBy.Trim(),
            Status = WorkflowInstanceStatus.InProgress
        };
        foreach (var (taskType, assignedRole) in tasks)
            instance.Tasks.Add(new WorkflowTask
            {
                Id = Guid.NewGuid(),
                TaskType = taskType.Trim(),
                AssignedRole = assignedRole.Trim()
            });
        db.Instances.Add(instance);
        await db.SaveChangesAsync(cancellationToken);
        return instance;
    }

    public async Task ClaimTaskAsync(
        Guid taskId, string actorId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        var task = await db.Tasks.Include(x => x.Instance)
            .SingleOrDefaultAsync(x => x.Id == taskId, cancellationToken)
            ?? throw new KeyNotFoundException("Workflow task was not found.");
        if (task.Status != WorkflowTaskStatus.Pending)
            throw new InvalidOperationException("Only pending tasks can be claimed.");
        if (string.Equals(task.Instance.SubmittedBy, actorId, StringComparison.Ordinal))
            throw new InvalidOperationException("Workflow submitters cannot claim their own approval tasks.");
        task.Status = WorkflowTaskStatus.Claimed;
        task.AssignedTo = actorId.Trim();
        db.Actions.Add(new WorkflowAction
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            Type = WorkflowActionType.Claimed,
            ActorId = actorId.Trim()
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DecideTaskAsync(
        Guid taskId, string actorId, bool approve, string? comment,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var task = await db.Tasks.Include(x => x.Instance)
            .SingleOrDefaultAsync(x => x.Id == taskId, cancellationToken)
            ?? throw new KeyNotFoundException("Workflow task was not found.");
        if (task.Status != WorkflowTaskStatus.Claimed ||
            !string.Equals(task.AssignedTo, actorId, StringComparison.Ordinal))
            throw new InvalidOperationException("Only the actor who claimed the task can decide it.");
        if (string.Equals(task.Instance.SubmittedBy, actorId, StringComparison.Ordinal))
            throw new InvalidOperationException("Workflow submitters cannot approve or reject their own tasks.");
        task.Status = approve ? WorkflowTaskStatus.Completed : WorkflowTaskStatus.Rejected;
        db.Actions.Add(new WorkflowAction
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            Type = approve ? WorkflowActionType.Approved : WorkflowActionType.Rejected,
            ActorId = actorId.Trim(),
            Comment = comment
        });
        if (!approve)
        {
            task.Instance.Status = WorkflowInstanceStatus.Rejected;
            task.Instance.CompletedAt = DateTimeOffset.UtcNow;
        }
        else if (!await db.Tasks.AnyAsync(x => x.InstanceId == task.InstanceId && x.Id != taskId
                     && x.Status != WorkflowTaskStatus.Completed, cancellationToken))
        {
            task.Instance.Status = WorkflowInstanceStatus.Approved;
            task.Instance.CompletedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
