using Brokerage.Common;

namespace Brokerage.Reporting;

public sealed class ReportingJsonRpcMethods : IJsonRpcMethodProvider
{
    public IEnumerable<JsonRpcMethod> GetMethods()
    {
        yield return new("reporting.queue", async call =>
        {
            call.RequireRole("BrokerageOperations", "Administrator");
            return await call.Service<ReportingService>().QueueReportAsync(
                call.Required<string>("reportType"), call.ActorId,
                call.OptionalString("parametersJson"), call.CancellationToken);
        }, RequiredRoles: ["BrokerageOperations", "Administrator"]);
        yield return new("reporting.audit", async call =>
            await call.Service<ReportingService>().RecordAuditEventAsync(
                call.ActorId, call.Required<string>("action"), call.Required<string>("entityType"),
                call.OptionalString("entityId"), call.OptionalString("correlationId"),
                call.OptionalString("payloadJson"), call.CancellationToken),
            RequiredRoles: ["Administrator"]);
    }
}
