using Brokerage.Host.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Brokerage.Host;

public static class FlowEndpoints
{
    public static IEndpointRouteBuilder MapFlowEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/rpc/flows", GetFlows)
            .RequireAuthorization()
            .WithName("GetBrokerageFlows")
            .WithSummary("List available brokerage JSON-RPC workflows")
            .WithDescription("Returns registered module workflows without requiring Chat provider configuration.")
            .Produces<IReadOnlyList<BrokerageFlowResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static Ok<IReadOnlyList<BrokerageFlowResponse>> GetFlows(JsonRpcMethodCatalog methods) =>
        TypedResults.Ok<IReadOnlyList<BrokerageFlowResponse>>(
            ChatFlowCatalog.Flows
                .Where(flow => methods.TryGetMethod(flow.Method, out _))
                .Select(flow => new BrokerageFlowResponse(
                    flow.Method,
                    flow.IsReadOnly,
                    flow.Description,
                    flow.Parameters,
                    WorkflowParameterCatalog.GetParameters(flow)))
                .ToArray());
}

/// <summary>Describes a registered brokerage module workflow available through JSON-RPC.</summary>
public sealed record BrokerageFlowResponse(
    string Method,
    bool IsReadOnly,
    string Description,
    string Parameters,
    IReadOnlyList<WorkflowParameter> Fields);
