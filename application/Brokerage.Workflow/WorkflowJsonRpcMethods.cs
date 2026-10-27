using Brokerage.Common;

namespace Brokerage.Workflow;

public sealed class WorkflowJsonRpcMethods : IJsonRpcMethodProvider
{
    private sealed record TaskInput(string TaskType, string AssignedRole);

    public IEnumerable<JsonRpcMethod> GetMethods()
    {
        yield return new("workflow.start", async call =>
        {
            var tasks = call.Required<List<TaskInput>>("tasks")
                .Select(x => (x.TaskType, x.AssignedRole)).ToArray();
            return await call.Service<WorkflowService>().StartWorkflowAsync(
                call.Required<Guid>("definitionId"), call.Required<string>("subjectType"),
                call.Required<string>("subjectId"), call.ActorId, tasks, call.CancellationToken);
        }, RequiredRoles: ["BrokerageOperations", "Administrator"]);
        yield return new("workflow.claimTask", async call =>
        {
            await call.Service<WorkflowService>().ClaimTaskAsync(
                call.Required<Guid>("taskId"), call.ActorId, call.CancellationToken);
            return null;
        });
        yield return new("workflow.decideTask", async call =>
        {
            await call.Service<WorkflowService>().DecideTaskAsync(
                call.Required<Guid>("taskId"), call.ActorId, call.Required<bool>("approve"),
                call.OptionalString("comment"), call.CancellationToken);
            return null;
        });
    }
}
