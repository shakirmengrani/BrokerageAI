# Brokerage Modular Monolith — Starter Scaffold

A starter .NET solution based on the supplied brokerage architecture. It uses a modular monolith, EF Core, PostgreSQL, and one schema/context per business module.

## Included

- `Brokerage.Host` — ASP.NET Core entry point, API endpoints, and serves the built `Brokerage.Web` SPA
- `Brokerage.Common` — shared actor-context abstraction and base audit interface
- Implemented modules: `Identity`, `Clients`, `Accounts`, `MarketData`, `Orders`, `Portfolios`, `Settlement`, `Risk`, `Billing`, `Reporting`, `Notifications`, `Payments`, `Reconciliation`, `Documents`, `Workflow`, `ReferenceData`, `Tax`, `CorporateActions`, `Integrations`, `Admin`
- Each module registers a scoped application service alongside its own EF Core context
- `CODING_STANDARDS.md`

## Requirements

- .NET 10 SDK
- PostgreSQL
- EF Core CLI (`dotnet tool install --global dotnet-ef --version 10.*`)

## Build

From this directory:

```bash
dotnet restore Brokerage.sln
dotnet build Brokerage.sln
```

## Development run

Configure the database connection string first (see [Database configuration](#database-configuration)). In the first terminal, run the Host:

```bash
ASPNETCORE_URLS=http://127.0.0.1:8080 dotnet run --project Brokerage.Host/Brokerage.Host.csproj
```

Before running the Host, set `Identity:JwtSigningKey` using user secrets as described in [Identity and authentication](#identity-and-authentication).

In a second terminal, start the Vite development server:

```bash
cd Brokerage.Web
npm ci
npm run dev
```

Open `http://127.0.0.1:5173` for the Dashboard or `http://127.0.0.1:5173/chat` for the Chat UI. Vite proxies `/api` and `/rpc` requests to the Host on port 8080. The Host also serves the production-built SPA directly; its API endpoints include `/api/chat`, `/api/rpc/flows`, `/rpc`, `/mcp`, and `/health`.

## Production build and run

Build the frontend assets, publish the Host, then run the published application. Set the required environment variables in the production environment or secret manager; do not put credentials in source control.

```bash
cd Brokerage.Web
npm ci
npm run build
cd ..
dotnet publish Brokerage.Host/Brokerage.Host.csproj -c Release -o ./artifacts/publish
```

Then, from the repository root, start the published Host:

```bash
ConnectionStrings__DefaultConnection='Host=<db-host>;Port=5432;Database=brokerage;Username=<db-user>;Password=<db-password>' \
Identity__JwtSigningKey='<random-secret-of-at-least-32-bytes>' \
Chat__SettingsEncryptionKey='<base64-encoded-random-32-byte-key>' \
ASPNETCORE_ENVIRONMENT=Production \
ASPNETCORE_URLS=http://0.0.0.0:8080 \
dotnet ./artifacts/publish/Brokerage.Host.dll
```

Chat provider settings are optional for the Dashboard and module workflows. To enable the Chat UI's AI assistant, configure `Chat__Endpoint`, `Chat__Model`, and, when required by the provider, `Chat__ApiKey` using the deployment secret manager or the administrator-only Settings page.

Terminate TLS at a trusted reverse proxy or configure HTTPS directly for the Host. Configure and verify production authentication and authorization before exposing the chat, RPC, or MCP endpoints.

The scaffold still does not implement trading, accounting, or regulatory controls. Add and test those features before connecting to real-money workflows.

## Database configuration

The Host reads `ConnectionStrings:DefaultConnection`. Set the environment variable `ConnectionStrings__DefaultConnection`, or store the connection string in user secrets:

```bash
dotnet user-secrets init --project Brokerage.Host
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=brokerage;Username=postgres;YOUR_PASSWORD" --project Brokerage.Host
```

Do not commit credentials.

## Identity and authentication

The Identity module supports email/password self-registration, sign-in, current-user profile, refresh-token rotation, and logout:

- `POST /api/identity/register` creates an account and returns bearer tokens.
- `POST /api/identity/login` verifies credentials and starts a session.
- `POST /api/identity/refresh` revokes the submitted refresh token and returns a replacement.
- `POST /api/identity/logout` revokes a refresh token.
- `GET /api/identity/me` returns the authenticated user and current database roles.

Passwords are hashed by ASP.NET Core's `PasswordHasher<User>`. Access tokens are HS256 JWTs valid for 15 minutes. Refresh tokens are random opaque values stored in the Identity database only as SHA-256 hashes and expire after 30 days. The refresh operation rotates each token once. The authentication endpoints are limited to 10 requests per minute per remote IP.

The Web client stores the signed-in user profile, access token, refresh token, and expiry times in browser `localStorage`, restores the session after reload, and rotates tokens when needed. Signing out clears these local values immediately and also asks the Host to revoke the refresh token; if the revocation request fails, the browser is still signed out and the UI reports that server-side revocation could not be confirmed. Because JavaScript can read `localStorage`, an XSS vulnerability could expose these tokens. For production deployments with a browser client, prefer a `Secure`, `HttpOnly`, `SameSite` cookie-based session design.

Configure `Identity__JwtSigningKey` with at least 32 bytes of randomly generated secret material, and keep it in a secret manager or user secrets. For development, generate a value with `openssl rand -hex 32`, then save it without committing it:

```bash
dotnet user-secrets init --project Brokerage.Host
dotnet user-secrets set "Identity:JwtSigningKey" "<generated-64-character-hex-value>" --project Brokerage.Host
```

Optional settings are `Identity__Issuer` and `Identity__Audience`; defaults are `Brokerage.Host` and `Brokerage.Api`. The Host refuses to start if the signing key is missing or too short. See [Identity.http](./Brokerage.Host/Identity.http) for endpoint examples.

The administrator-only chat provider Settings page stores provider credentials encrypted in the Identity database. The Host requires `Chat:SettingsEncryptionKey`, a Base64-encoded random 32-byte key; keep it separate from the JWT signing key and preserve the same value across restarts and Host replicas:

```bash
dotnet user-secrets set "Chat:SettingsEncryptionKey" "$(openssl rand -base64 32)" --project Brokerage.Host
```

In production, set `Chat__SettingsEncryptionKey` from the deployment secret manager. Losing or rotating this key without first re-encrypting the stored provider API key makes that key unreadable. Apply the Identity EF migration `20261027151500_AddChatProviderSettings` before using this page.

Self-registered users receive no privileged roles. To create the initial superadmin, configure both `Identity:Bootstrap:Email` and `Identity:Bootstrap:Password` using a secret manager, environment variables, or local user-secrets. For local development:

```bash
dotnet user-secrets set "Identity:Bootstrap:Email" "<admin-email>" --project Brokerage.Host
dotnet user-secrets set "Identity:Bootstrap:Password" "<unique-random-password-of-at-least-12-characters>" --project Brokerage.Host
```

On startup, when no active user has the `Administrator` role, the Host creates that role and assigns it to the configured account. This is the application's superadmin role. The operation is serialized across Host instances and is safe to repeat: it does not reset an existing account's password, and if the configured email already exists its password must match the configured secret. Once an active administrator exists, bootstrap makes no changes. Omit both settings to disable bootstrap; configuring only one causes startup to fail. The Identity database schema must already be provisioned. Do not use a shared or hard-coded default password, and remove bootstrap secrets after the first administrator has been created.

Migration `20261027153000_GrantAdministratorToExistingUsers` creates the `Administrator` role if needed and assigns it to every account that exists when the migration runs. This includes inactive accounts; new registrations do not inherit this role. Because this grants superadmin privileges broadly, revoke access individually after reviewing the accounts. The migration is intentionally not reversible automatically.

Email verification, password reset, MFA, and global logout/access-token revocation are not implemented yet. A revoked refresh token cannot mint new tokens, but an already-issued access token remains valid until its 15-minute expiry.

The host currently includes a basic health endpoint. Module DbContexts are isolated in their own projects and schemas.

All 20 module contexts (including Identity) have an initial EF migration checked in. Each context uses a separate `__EFMigrationsHistory` table in its module schema. Configure `ConnectionStrings:DefaultConnection` before starting the host; startup fails explicitly if it is missing. Apply future migrations to each context with `dotnet ef database update --context <ContextName> --project Brokerage.<Module>/Brokerage.<Module>.csproj --startup-project Brokerage.Host/Brokerage.Host.csproj` after adding the EF design-time tooling required by `dotnet-ef`.

The initial application services provide module-local operations such as client registration and KYC review, balanced journal posting, quote recording, order lifecycle updates, payment status tracking, report/audit persistence, workflow approvals, and reference-data maintenance. They are not complete production workflows: wire trusted identity and authorization into host/API handlers, implement external provider and storage adapters, and add concurrency controls, migrations, integration tests, and applicable regulatory safeguards before production or real-money use. Integrations store references to secrets, not secret material.

## JSON-RPC

The host exposes JSON-RPC 2.0 over HTTP POST at `/rpc`. Requests use JSON objects for named parameters; batch requests and notifications are supported. The endpoint caps request bodies at 1 MiB and batch size at 20. Protocol errors use the standard JSON-RPC codes; business/authorization errors use server error codes in the `-32000` range.

Example:

```bash
curl -X POST http://localhost:5000/rpc \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"system.ping","id":1}'
```

Each business module owns its JSON-RPC method provider and registers it with that module's service collection. The Host aggregates those providers in `JsonRpcMethodCatalog`; `JsonRpcMiddleware` handles HTTP routing, protocol validation, authentication/authorization, and responses. Dotted method names include `clients.register`, `accounts.balance`, `orders.place`, `payments.create`, and `workflow.decideTask`.

All methods other than `system.ping` require an authenticated principal. The Host validates Identity-module bearer JWTs. Authorization reads the actor from the JWT subject, uses `account_id` and `client_id` claims for scoped access, and checks roles including `Administrator`, `BrokerageOperations`, `BrokerageTrader`, and `ComplianceOfficer`. Never trust actor, account, or client identity supplied only in JSON parameters. Configure and test fine-grained authorization, rate limits, and audit policy before exposing the endpoint to clients.

## MCP

The Host exposes an authenticated Model Context Protocol (MCP) Streamable HTTP endpoint at `/mcp`, implemented by the `Brokerage.Mcp` adapter using the official .NET MCP SDK. The adapter currently publishes two read-only tools: `accounts_get_balance` (enforces the caller's `account_id` claims, with administrator and brokerage-operations access) and `market_data_get_latest_quote`. Both tools call the existing module application services; the adapter contains no brokerage business rules. MCP sessions are stateless.

Authenticate MCP requests with a bearer access token from the Identity endpoints. The endpoint and tools require an authenticated principal. Order placement, cancellation, payments, and other state-changing actions are not exposed as MCP tools until an explicit authorization and user-confirmation flow is implemented.

## Chat API

The authenticated `POST /api/chat` endpoint accepts a message and up to 20 prior user/assistant messages. The Host uses a configured OpenAI-compatible Chat Completions endpoint and exposes the registered JSON-RPC workflows from all 20 modules as chat tools: Accounts, Admin, Billing, Clients, Corporate Actions, Documents, Identity, Integrations, Market Data, Notifications, Orders, Payments, Portfolios, Reconciliation, Reference Data, Reporting, Risk, Settlement, Tax, and Workflow. Examples include account and client lookups, balance and risk checks, stored quotes, workflow and reconciliation operations, orders, payments, reporting, and reference data. The existing JSON-RPC method handlers remain the source of module behavior and authorization; chat invokes them with the authenticated caller's principal.

Authenticated Dashboard and Chat clients can retrieve registered workflow metadata from `GET /api/rpc/flows`; the existing `GET /api/chat/flows` remains available for compatibility. Each entry includes its method, description, parameter summary, read/write mode, and structured field definitions (type, requiredness, and select options) used to generate workflow forms.

Read-only workflows run immediately, subject to the same role and account/client checks as JSON-RPC. State-changing workflows never run just because the model requested them: chat returns a user-bound draft with its method and arguments. The UI's **Confirm and execute** button calls `POST /api/chat/actions/{id}/confirm` with an antiforgery token; **Discard** calls `DELETE /api/chat/actions/{id}`. Confirmation rechecks method role/record permissions and consumes the draft once. The response is `{ "reply": "...", "pendingActions": [...] }`.

Drafts expire after 10 minutes and currently live only in the Host process memory. They are lost on restart and require requests to reach the same Host instance, so use a durable shared draft store before deploying multiple replicas or relying on drafts across restarts. Drafts require the authenticated principal to have a stable `NameIdentifier` claim. Only the methods currently registered through module JSON-RPC providers are available to chat; module service methods without a registered RPC method are not exposed.

Configure an OpenAI-compatible provider in the administrator-only **Settings** page, or set these Host settings as environment variables:

- `Chat__Endpoint` — full chat completions URL, such as `https://api.openai.com/v1/chat/completions`
- `Chat__Model` — model identifier supported by the provider
- `Chat__ApiKey` — provider API key (optional for local compatible providers; keep secrets outside source control)

Saved provider settings override environment configuration and take effect on the next chat request. The API key is encrypted before database storage and is never returned by the settings endpoint. Updating the endpoint/model without entering a replacement key preserves the existing key. Only users with the `Administrator` role can access the page or its API. If endpoint or model is missing, chat returns `503`; provider failures return `502`. OpenAPI documentation is available at `/openapi/v1.json` in Development. See [Chat.http](./Brokerage.Host/Chat.http) for request examples.

## Web UI

`Brokerage.Web` is a single React + TypeScript frontend. The default `/` route is the module Dashboard; `/chat` opens the responsive, terminal-inspired Chat UI. Both routes share the same Identity session. It requires Node.js 20.19+ or 22.12+. The development and production commands are listed above. In production, Vite writes the built assets to `Brokerage.Host/wwwroot`; the Host serves the SPA and falls back to `index.html` for both client-side routes.

The Dashboard loads registered workflow metadata from the authenticated `GET /api/rpc/flows` endpoint and invokes module workflows through authenticated JSON-RPC `POST /rpc`, without requiring any Chat provider configuration. The Dashboard and the Chat flow catalog use the same generated, typed forms; nested JSON inputs are validated before submission. Read-only operations can be run directly. State-changing operations require a separate confirmation step; the Host continues to enforce authentication and module authorization. See [Flows.http](./Brokerage.Host/Flows.http) for request examples.
