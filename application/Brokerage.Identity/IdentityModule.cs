using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Brokerage.Common;

namespace Brokerage.Identity;

/// <summary>
/// Composition entry point for Brokerage.Identity.
/// </summary>
public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<IdentityDb>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDb.Schema)));
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IdentityService>();
        services.AddSingleton<IJsonRpcMethodProvider, IdentityJsonRpcMethods>();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);

        return services;
    }
}
