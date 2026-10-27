using System.Security.Claims;
using System.Threading.RateLimiting;
using Brokerage.Accounts;
using Brokerage.Admin;
using Brokerage.Billing;
using Brokerage.Clients;
using Brokerage.CorporateActions;
using Brokerage.Documents;
using Brokerage.Identity;
using Brokerage.Integrations;
using Brokerage.MarketData;
using Brokerage.Notifications;
using Brokerage.Orders;
using Brokerage.Payments;
using Brokerage.Portfolios;
using Brokerage.Reconciliation;
using Brokerage.ReferenceData;
using Brokerage.Reporting;
using Brokerage.Risk;
using Brokerage.Settlement;
using Brokerage.Tax;
using Brokerage.Workflow;
using Brokerage.Host;
using Brokerage.Mcp;
using Brokerage.Host.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "The database connection string 'ConnectionStrings:DefaultConnection' must be configured.");
}

var identityTokenOptions = IdentityTokenOptions.FromConfiguration(builder.Configuration);
var chatSettingsEncryptionKey = builder.Configuration["Chat:SettingsEncryptionKey"];
byte[] decodedChatSettingsEncryptionKey;
try
{
    decodedChatSettingsEncryptionKey = Convert.FromBase64String(chatSettingsEncryptionKey ?? string.Empty);
}
catch (FormatException exception)
{
    throw new InvalidOperationException(
        "Chat:SettingsEncryptionKey must be a Base64-encoded 32-byte key.",
        exception);
}

if (decodedChatSettingsEncryptionKey.Length != 32)
{
    throw new InvalidOperationException(
        "Chat:SettingsEncryptionKey must be a Base64-encoded 32-byte key.");
}

var bootstrapAdministratorEmail = builder.Configuration["Identity:Bootstrap:Email"];
var bootstrapAdministratorPassword = builder.Configuration["Identity:Bootstrap:Password"];
if (string.IsNullOrWhiteSpace(bootstrapAdministratorEmail) !=
    string.IsNullOrWhiteSpace(bootstrapAdministratorPassword))
{
    throw new InvalidOperationException(
        "Identity:Bootstrap:Email and Identity:Bootstrap:Password must either both be configured or both be omitted.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = identityTokenOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = identityTokenOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = identityTokenOptions.SigningKey,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddValidation();
builder.Services.AddSingleton(identityTokenOptions);
builder.Services.AddSingleton<IdentityTokenService>();
builder.Services.AddScoped<ChatProviderSettingsService>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("identity-auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});
builder.Services.AddOpenApi();
builder.Services.AddHttpClient<IChatService, OpenAiCompatibleChatService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services
    .AddIdentityModule(connectionString)
    .AddClientsModule(connectionString)
    .AddAccountsModule(connectionString)
    .AddMarketDataModule(connectionString)
    .AddOrdersModule(connectionString)
    .AddPortfoliosModule(connectionString)
    .AddSettlementModule(connectionString)
    .AddRiskModule(connectionString)
    .AddBillingModule(connectionString)
    .AddReportingModule(connectionString)
    .AddNotificationsModule(connectionString)
    .AddPaymentsModule(connectionString)
    .AddReconciliationModule(connectionString)
    .AddDocumentsModule(connectionString)
    .AddWorkflowModule(connectionString)
    .AddReferenceDataModule(connectionString)
    .AddTaxModule(connectionString)
    .AddCorporateActionsModule(connectionString)
    .AddIntegrationsModule(connectionString)
    .AddAdminModule(connectionString);

builder.Services.AddHealthChecks();
builder.Services.AddSingleton<JsonRpcMethodCatalog>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ChatActionDraftStore>();
builder.Services.AddBrokerageMcp();

var app = builder.Build();

if (!string.IsNullOrWhiteSpace(bootstrapAdministratorEmail))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider
        .GetRequiredService<IdentityService>()
        .EnsureBootstrapAdministratorAsync(
            bootstrapAdministratorEmail,
            bootstrapAdministratorPassword!);
    app.Logger.LogInformation(
        "Bootstrap administrator setup is complete. The account receives the Administrator role.");
}

app.UseRateLimiter();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/mcp", StringComparison.OrdinalIgnoreCase) &&
        context.User.Identity?.IsAuthenticated != true &&
        await context.RequestServices
            .GetRequiredService<IAuthenticationSchemeProvider>()
            .GetDefaultChallengeSchemeAsync() is null)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    if (context.Request.Path.StartsWithSegments("/api/chat", StringComparison.OrdinalIgnoreCase))
    {
        if (context.Request.ContentLength > 131_072)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        var bodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodySizeFeature is { IsReadOnly: false })
            bodySizeFeature.MaxRequestBodySize = 131_072;

        if (context.User.Identity?.IsAuthenticated != true &&
            await context.RequestServices
                .GetRequiredService<IAuthenticationSchemeProvider>()
                .GetDefaultChallengeSchemeAsync() is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
    }

    await next();
});
app.UseAuthorization();
app.UseMiddleware<JsonRpcMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHealthChecks("/health");
app.MapIdentityEndpoints();
app.MapBrokerageMcp("/mcp");
app.MapChatEndpoints();
app.MapChatSettingsEndpoints();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();
app.MapFallbackToFile("index.html");

app.Run();
