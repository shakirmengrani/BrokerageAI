# Brokerage System Architecture

For your brokerage system, I recommend a modular architecture where each business module owns its domain models, services, database context, and schema, while `Brokerage.Host` handles shared infrastructure such as RPC, health checks, and idempotency.

The proposed technology stack is:

- **Framework:** .NET
- **ORM:** Entity Framework Core (EF Core)
- **Database:** PostgreSQL
- **Architecture:** Modular monolith with independent business modules
- **Persistence:** Separate `DbContext` and migrations per module

## 1. Core Brokerage Modules

### 1.1 Identity & Access Management — `Brokerage.Identity`

**Responsibilities:**
- Users, roles, and permissions
- Authentication and authorization
- Session management and API keys
- Multi-factor authentication (MFA)
- Password policies

**Tables:**
- `users`
- `roles`
- `permissions`
- `user_roles`
- `sessions`

### 1.2 Client & CRM — `Brokerage.Clients`

**Responsibilities:**
- Client profiles and relationships
- Know Your Customer (KYC)
- Client onboarding
- Suitability assessments
- Beneficial ownership
- Documents and contacts
- Client classifications

**Tables:**
- `clients`
- `client_documents`
- `client_contacts`
- `kyc_records`

### 1.3 Accounts & Ledger — `Brokerage.Accounts`

**Responsibilities:**
- Trading accounts
- Cash balances
- Account restrictions
- Double-entry bookkeeping
- Deposits and withdrawals
- Account statements

**Tables:**
- `accounts`
- `ledger_entries`
- `journal_entries`
- `cash_movements`

### 1.4 Orders & Execution — `Brokerage.Orders`

**Responsibilities:**
- Buy and sell orders
- Order validation
- Order routing
- Amendments and cancellations
- Execution reports
- Order state transitions

**Tables:**
- `orders`
- `executions`
- `order_events`

### 1.5 Portfolio & Holdings — `Brokerage.Portfolios`

**Responsibilities:**
- Securities owned by clients
- Positions and average cost
- Realized and unrealized profit/loss
- Portfolio valuations

**Tables:**
- `portfolios`
- `positions`
- `position_lots`
- `valuations`

### 1.6 Instruments & Market Data — `Brokerage.MarketData`

**Responsibilities:**
- Stocks, bonds, ETFs, and contracts
- Instrument symbols and exchanges
- Market prices and quotes
- Historical price bars
- Trading sessions
- Corporate actions

**Tables:**
- `instruments`
- `exchanges`
- `quotes`
- `price_bars`
- `corporate_actions`

### 1.7 Clearing & Settlement — `Brokerage.Settlement`

**Responsibilities:**
- Trade confirmations
- Settlement obligations
- Settlement dates
- Cash and securities movements
- Reconciliation
- Failed settlement handling

**Tables:**
- `settlement_instructions`
- `settlement_batches`
- `settlement_failures`

### 1.8 Risk & Compliance — `Brokerage.Risk`

**Responsibilities:**
- Buying power calculations
- Margin requirements
- Exposure limits
- Suitability checks
- Trading restrictions
- Suspicious activity monitoring
- Compliance cases

**Tables:**
- `risk_limits`
- `margin_requirements`
- `alerts`
- `compliance_cases`

### 1.9 Fees & Commissions — `Brokerage.Billing`

**Responsibilities:**
- Brokerage commissions
- Fee schedules
- Exchange fees and taxes
- Invoices and rebates
- Fee calculations

**Tables:**
- `fee_schedules`
- `commission_rules`
- `charges`
- `invoices`

### 1.10 Reporting & Audit — `Brokerage.Reporting`

**Responsibilities:**
- Trade confirmations
- Account statements
- Operational reports
- Immutable audit events
- Regulatory reporting

**Tables:**
- `audit_events`
- `report_jobs`
- `report_artifacts`

## 2. Supporting Modules

| Module | Suggested Project | Responsibilities |
|---|---|---|
| Notifications | `Brokerage.Notifications` | Email, SMS, push notifications, and trade alerts |
| Payments | `Brokerage.Payments` | Bank transfers, deposits, withdrawals, and payment status |
| Reconciliation | `Brokerage.Reconciliation` | Reconcile internal books with banks, exchanges, and custodians |
| Documents | `Brokerage.Documents` | Secure document storage, versioning, and retention |
| Workflow | `Brokerage.Workflow` | Approvals, maker-checker controls, and review queues |
| Reference Data | `Brokerage.ReferenceData` | Currencies, countries, calendars, and business classifications |
| Tax | `Brokerage.Tax` | Withholding, tax lots, and tax statements |
| Corporate Actions | `Brokerage.CorporateActions` | Dividends, splits, rights issues, and mergers |
| Integrations | `Brokerage.Integrations` | Exchange, custodian, banking, and market data adapters |
| Administration | `Brokerage.Admin` | Tenant settings, configuration, and operational controls |

Some supporting modules can initially live inside larger modules. For example, corporate actions can start within `MarketData` or `Portfolios`, while reconciliation can initially be part of `Settlement`.

## 3. Suggested .NET Solution Structure

Organize the solution into a thin host, shared infrastructure, and independent business modules.

```text
Brokerage.sln
│
├── Brokerage.Host/
│   ├── RPC endpoints
│   ├── Middleware
│   ├── Health checks
│   ├── Idempotency
│   ├── Dependency injection
│   └── Program.cs
│
├── Brokerage.Common/
│   ├── Abstractions/
│   ├── Primitives/
│   └── BaseTypes/
│
├── Brokerage.Identity/
├── Brokerage.Clients/
├── Brokerage.Accounts/
├── Brokerage.Orders/
├── Brokerage.Portfolios/
├── Brokerage.MarketData/
├── Brokerage.Settlement/
├── Brokerage.Risk/
├── Brokerage.Billing/
├── Brokerage.Reporting/
│
├── Brokerage.Notifications/
├── Brokerage.Payments/
├── Brokerage.Reconciliation/
├── Brokerage.Documents/
├── Brokerage.Workflow/
├── Brokerage.ReferenceData/
├── Brokerage.Tax/
├── Brokerage.CorporateActions/
├── Brokerage.Integrations/
└── Brokerage.Admin/
```

### 3.1 Internal Module Structure

Each module can follow a consistent internal structure.

Example: `Brokerage.Orders`

```text
Brokerage.Orders/
├── Models/
├── Services/
├── Contracts/
├── Persistence/
│   ├── OrdersDb.cs
│   └── Migrations/
├── OrdersModule.cs
└── Brokerage.Orders.csproj
```

You do not necessarily need a separate project for every folder. Start with class libraries for modules that have clear domain boundaries.

### 3.2 Module Responsibilities

**`Brokerage.Host`**
- Application entry point
- RPC endpoint registration
- Middleware and shared infrastructure
- Health checks
- Idempotency infrastructure
- Module registration

**`Brokerage.Common`**
- Shared primitives
- Common abstractions
- Base types
- Cross-cutting contracts

Avoid placing business logic in `Brokerage.Common`.

**Business modules**
- Own their domain models
- Implement business rules and services
- Own their `DbContext`
- Manage their own migrations
- Expose explicit application contracts

## 4. Database Ownership Strategy

For PostgreSQL and EF Core, I recommend starting with a single PostgreSQL database containing separate schemas and migration histories for each module.

| Context | Schema | Example Tables |
|---|---|---|
| `Db` | `public` | `idempotency_record` |
| `IdentityDb` | `identity` | `users`, `roles` |
| `ClientsDb` | `clients` | `clients`, `kyc_records` |
| `AccountsDb` | `accounts` | `accounts`, `ledger_entries` |
| `OrdersDb` | `orders` | `orders`, `executions` |
| `PortfoliosDb` | `portfolios` | `positions`, `valuations` |
| `MarketDataDb` | `market_data` | `instruments`, `quotes` |
| `SettlementDb` | `settlement` | `settlement_instructions` |

### 4.1 Database Rules

Each module should:

1. Own its tables and migrations.
2. Register its own `DbContext`.
3. Expose business operations through services or module contracts.
4. Avoid querying another module's tables directly through its own context.
5. Communicate across modules through application services or domain/integration events.
6. Use explicit transaction and consistency strategies for cross-module workflows.

A shared PostgreSQL database does not mean all modules need to share one `DbContext`.

### 4.2 EF Core Configuration Example

```csharp
builder.Services.AddDbContext<OrdersDb>(options =>
    options.UseNpgsql(
        connectionString,
        npgsql => npgsql.MigrationsHistoryTable(
            "__EFMigrationsHistory",
            "orders")));
```

Configure the schema explicitly in the context:

```csharp
protected override void OnModelCreating(
    ModelBuilder modelBuilder)
{
    modelBuilder.HasDefaultSchema("orders");

    base.OnModelCreating(modelBuilder);
}
```

Use the equivalent configuration for each module, changing the schema and migration history as appropriate.

## 5. Recommended Implementation Order

For an initial brokerage platform, implement the modules in the following order:

1. **Identity** — Users, roles, permissions, and secure authentication.
2. **Clients** — Onboarding, KYC, and client eligibility.
3. **Accounts & Ledger** — Accounts, double-entry bookkeeping, and cash movements.
4. **Instruments & Market Data** — Securities, instruments, and prices.
5. **Orders** — Order lifecycle and execution handling.
6. **Portfolios** — Positions derived from executions and corporate actions.
7. **Risk** — Buying power, limits, and pre-trade checks.
8. **Settlement & Reconciliation** — Post-trade processing and financial controls.
9. **Billing, Reporting & Notifications** — Commissions, statements, and client communications.

Real-money trading requires additional safeguards around ledger correctness, authorization, auditability, regulatory requirements, and failure recovery. Exact module boundaries depend on the target market, asset classes, and applicable regulations.

## 6. Final Recommendation

Start with these five modules:

- `Brokerage.Identity`
- `Brokerage.Clients`
- `Brokerage.Accounts`
- `Brokerage.MarketData`
- `Brokerage.Orders`

Keep `Brokerage.Host` thin and let each module own its `DbContext`, database schema, migrations, and business logic.

This modular architecture provides a clear separation of concerns while allowing the brokerage platform to evolve incrementally without introducing unnecessary microservice complexity at the beginning.
