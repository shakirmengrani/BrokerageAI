using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Brokerage.Common;

namespace Brokerage.Orders;

/// <summary>
/// Composition entry point for Brokerage.Orders.
/// </summary>
public static class OrdersModule
{
    public static IServiceCollection AddOrdersModule(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<OrdersDb>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", OrdersDb.Schema)));
        services.AddScoped<OrdersService>();
        services.AddSingleton<IJsonRpcMethodProvider, OrdersJsonRpcMethods>();

        return services;
    }
}
