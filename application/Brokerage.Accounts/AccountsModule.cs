using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Brokerage.Common;

namespace Brokerage.Accounts;

/// <summary>
/// Composition entry point for Brokerage.Accounts.
/// </summary>
public static class AccountsModule
{
    public static IServiceCollection AddAccountsModule(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<AccountsDb>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", AccountsDb.Schema)));
        services.AddScoped<AccountsService>();
        services.AddSingleton<IJsonRpcMethodProvider, AccountsJsonRpcMethods>();

        return services;
    }
}
