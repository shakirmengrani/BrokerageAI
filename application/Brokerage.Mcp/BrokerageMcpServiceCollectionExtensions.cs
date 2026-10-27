using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Brokerage.Mcp;

public static class BrokerageMcpServiceCollectionExtensions
{
    public static IServiceCollection AddBrokerageMcp(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<BrokerageToolService>();
        services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = "brokerage-mcp",
                    Version = "1.0.0"
                };
            })
            .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
            .WithTools<BrokerageTools>();

        return services;
    }

    public static IEndpointConventionBuilder MapBrokerageMcp(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/mcp") =>
        endpoints.MapMcp(pattern).RequireAuthorization();
}
