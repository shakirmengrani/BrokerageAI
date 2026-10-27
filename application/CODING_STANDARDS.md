# Brokerage System — Coding Standards

## Architecture
- Use a modular monolith initially.
- Each business module owns its models, services, contracts, `DbContext`, schema, and migrations.
- Keep `Brokerage.Host` thin: startup, RPC/HTTP infrastructure, health checks, dependency injection, and cross-cutting middleware.
- Keep `Brokerage.Common` limited to genuinely shared abstractions and primitives.
- Do not query another module's tables directly through a module-owned context.

## C# conventions
- Types, methods, properties, and constants: `PascalCase`.
- Locals and parameters: `camelCase`.
- Private fields: `_camelCase`.
- Async methods end with `Async`.
- RPC names use lowercase dotted names, e.g. `clients.register`.
- Database tables and columns use `snake_case`.
- Nullable reference types are enabled; do not ignore warnings without a reason.

## Services
- Follow the existing handler convention: `Task<JsonNode?> MethodAsync(JsonObject? args, IServiceProvider sp, CancellationToken ct)`.
- Inject the module `DbContext` through the constructor.
- Pass cancellation tokens to asynchronous I/O and EF Core calls.
- Use `AsNoTracking()` for read-only queries.
- Validate all input and enforce authorization server-side.
- Get actor IDs from the trusted execution context, never from request payloads.
- Keep RPC registration classes declarative.

## Models and persistence
- Configure table names, lengths, precision, indexes, constraints, and delete behavior explicitly.
- Use `DateTimeOffset` for timestamps and `DateOnly` for date-only values.
- Use `decimal` for financial amounts.
- Use database constraints for invariants where possible.
- Use EF Core migrations; create new migrations rather than editing applied migrations.
- Use transactions for multi-step changes that must be atomic.

## Audit and security
- Use `CreatedAt`, `CreatedBy`, `UpdatedAt`, and `UpdatedBy` where appropriate.
- Use UTC timestamps.
- Never log passwords, tokens, or unnecessary sensitive personal data.
- Require authorization for protected operations.
- Avoid returning internal exception details to clients.

## Financial controls
- Balance journal debits and credits before posting.
- Filter balances by currency.
- Prevent duplicate/concurrent posting.
- Make posted entries immutable; use reversal entries for corrections.
- Validate accounting periods, permissions, and reconciliation requirements before production use.

## Quality gates
Before merging, run `dotnet format`, `dotnet build`, and `dotnet test`.
Add tests for validation, authorization, audit fields, database constraints, rollback, concurrency, and financial invariants.
