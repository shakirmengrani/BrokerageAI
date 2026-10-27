using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Brokerage.Common;

namespace Brokerage.Clients;

/// <summary>
/// Composition entry point for Brokerage.Clients.
/// </summary>
public static class ClientsModule
{
    public static IServiceCollection AddClientsModule(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<ClientsDb>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ClientsDb.Schema)));
        services.AddScoped<ClientsService>();
        services.AddSingleton<IJsonRpcMethodProvider, ClientsJsonRpcMethods>();

        return services;
    }
}
