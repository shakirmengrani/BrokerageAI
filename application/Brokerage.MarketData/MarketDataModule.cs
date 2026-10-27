using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Brokerage.Common;

namespace Brokerage.MarketData;

/// <summary>
/// Composition entry point for Brokerage.MarketData.
/// </summary>
public static class MarketDataModule
{
    public static IServiceCollection AddMarketDataModule(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<MarketDataDb>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", MarketDataDb.Schema)));
        services.AddScoped<MarketDataService>();
        services.AddSingleton<IJsonRpcMethodProvider, MarketDataJsonRpcMethods>();

        return services;
    }
}
