using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Brokerage.Common;

namespace Brokerage.Workflow;

public enum WorkflowInstanceStatus { Pending, InProgress, Approved, Rejected, Cancelled }
public enum WorkflowTaskStatus { Pending, Claimed, Completed, Rejected, Cancelled }
public enum WorkflowActionType { Submitted, Claimed, Approved, Rejected, Reassigned, Cancelled }

public sealed class WorkflowDefinition
{
    public Guid Id { get; init; }
    public required string Name { get; set; }
    public required string Version { get; set; }
    public bool IsActive { get; set; } = true;
    public string? DefinitionJson { get; set; }
    public ICollection<WorkflowInstance> Instances { get; } = [];
}

public sealed class WorkflowInstance
{
    public Guid Id { get; init; }
    public Guid DefinitionId { get; init; }
    public required string SubjectType { get; set; }
    public required string SubjectId { get; set; }
    public required string SubmittedBy { get; set; }
    public WorkflowInstanceStatus Status { get; set; } = WorkflowInstanceStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public WorkflowDefinition Definition { get; init; } = null!;
    public ICollection<WorkflowTask> Tasks { get; } = [];
}

public sealed class WorkflowTask
{
    public Guid Id { get; init; }
    public Guid InstanceId { get; init; }
    public required string TaskType { get; set; }
    public required string AssignedRole { get; set; }
    public string? AssignedTo { get; set; }
    public WorkflowTaskStatus Status { get; set; } = WorkflowTaskStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DueAt { get; set; }
    public WorkflowInstance Instance { get; init; } = null!;
    public ICollection<WorkflowAction> Actions { get; } = [];
}

public sealed class WorkflowAction
{
    public Guid Id { get; init; }
    public Guid TaskId { get; init; }
    public WorkflowActionType Type { get; set; }
    public required string ActorId { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public WorkflowTask Task { get; init; } = null!;
}

public sealed class WorkflowDb(DbContextOptions<WorkflowDb> options) : DbContext(options)
{
    public const string Schema = "workflow";
    public DbSet<WorkflowDefinition> Definitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowInstance> Instances => Set<WorkflowInstance>();
    public DbSet<WorkflowTask> Tasks => Set<WorkflowTask>();
    public DbSet<WorkflowAction> Actions => Set<WorkflowAction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<WorkflowDefinition>(e =>
        {
            e.ToTable("workflow_definitions"); e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.Property(x => x.Version).HasMaxLength(32).IsRequired();
            e.Property(x => x.DefinitionJson).HasColumnType("jsonb");
            e.HasIndex(x => new { x.Name, x.Version }).IsUnique();
        });
        modelBuilder.Entity<WorkflowInstance>(e =>
        {
            e.ToTable("workflow_instances"); e.HasKey(x => x.Id);
            e.Property(x => x.SubjectType).HasMaxLength(64).IsRequired();
            e.Property(x => x.SubjectId).HasMaxLength(128).IsRequired();
            e.Property(x => x.SubmittedBy).HasMaxLength(128).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasIndex(x => new { x.SubjectType, x.SubjectId, x.CreatedAt });
            e.HasOne(x => x.Definition).WithMany(x => x.Instances).HasForeignKey(x => x.DefinitionId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<WorkflowTask>(e =>
        {
            e.ToTable("workflow_tasks"); e.HasKey(x => x.Id);
            e.Property(x => x.TaskType).HasMaxLength(64).IsRequired();
            e.Property(x => x.AssignedRole).HasMaxLength(64).IsRequired();
            e.Property(x => x.AssignedTo).HasMaxLength(128);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasIndex(x => new { x.Status, x.AssignedRole, x.DueAt });
            e.HasOne(x => x.Instance).WithMany(x => x.Tasks).HasForeignKey(x => x.InstanceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<WorkflowAction>(e =>
        {
            e.ToTable("workflow_actions"); e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.ActorId).HasMaxLength(128).IsRequired();
            e.Property(x => x.Comment).HasMaxLength(2048);
            e.HasIndex(x => new { x.TaskId, x.OccurredAt });
            e.HasOne(x => x.Task).WithMany(x => x.Actions).HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

public static class WorkflowModule
{
    public static IServiceCollection AddWorkflowModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<WorkflowDb>(o => o.UseNpgsql(connectionString, p => p.MigrationsHistoryTable("__EFMigrationsHistory", WorkflowDb.Schema)));
        services.AddScoped<WorkflowService>();
        services.AddSingleton<IJsonRpcMethodProvider, WorkflowJsonRpcMethods>();
        return services;
    }
}
