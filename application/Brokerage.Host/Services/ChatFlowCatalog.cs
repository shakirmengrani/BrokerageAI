namespace Brokerage.Host.Services;

public sealed record ChatFlow(
    string Method,
    bool IsReadOnly,
    string Description,
    string Parameters);

public static class ChatFlowCatalog
{
    public static IReadOnlyList<ChatFlow> Flows { get; } =
    [
        new("accounts.open", false, "Open a brokerage account for a client.", "clientId (UUID), accountNumber, currency"),
        new("accounts.balance", true, "Read an account's posted ledger balance.", "accountId (UUID)"),
        new("accounts.createJournal", false, "Prepare a balanced ledger journal.", "accountId (UUID), reference, entries"),
        new("accounts.postJournal", false, "Post an existing ledger journal.", "journalId (UUID)"),
        new("admin.setOperationalControl", false, "Change an operational control.", "controlKey, isEnabled, optional reason"),
        new("admin.operationalControls", true, "List operational controls.", "no parameters"),
        new("billing.calculateCommission", true, "Calculate commission for a notional amount.", "feeScheduleId (UUID), instrumentType, notional"),
        new("billing.createCharge", false, "Create an account charge.", "accountId (UUID), reference, chargeType, amount, currency, optional orderId (UUID)"),
        new("clients.register", false, "Register a client.", "externalReference, givenName, familyName, email"),
        new("clients.startKyc", false, "Start KYC for a client.", "clientId (UUID)"),
        new("clients.reviewKyc", false, "Record a KYC review decision.", "recordId (UUID), decision"),
        new("clients.get", true, "Read a client record.", "clientId (UUID)"),
        new("corporateActions.recordElection", false, "Record a corporate-action election.", "actionId (UUID), accountId (UUID), electionOption, optional quantity"),
        new("documents.register", false, "Register document metadata.", "documentType, storageKey, contentType, sizeBytes, optional clientId (UUID) and sha256"),
        new("identity.createRole", false, "Create an identity role.", "name"),
        new("identity.assignRole", false, "Assign a role to a user.", "userId (UUID), roleId (UUID)"),
        new("integrations.enqueue", false, "Queue an integration message.", "endpointId (UUID), messageReference, messageType, direction, optional payloadJson"),
        new("marketData.latestQuote", true, "Read the latest stored quote for an instrument.", "instrumentId (UUID)"),
        new("marketData.recordQuote", false, "Record a market quote.", "instrumentId (UUID), bid, ask, observedAt (ISO 8601 timestamp)"),
        new("notifications.enqueue", false, "Queue a notification.", "channel, destination, template, optional clientId (UUID) and payloadJson"),
        new("notifications.queued", true, "List queued notifications.", "optional limit"),
        new("orders.place", false, "Prepare an order placement.", "accountId (UUID), instrumentId (UUID), clientOrderReference, side, type, timeInForce, quantity, optional limitPrice"),
        new("orders.cancel", false, "Cancel an existing order.", "orderId (UUID)"),
        new("orders.accept", false, "Accept an order.", "orderId (UUID)"),
        new("payments.create", false, "Prepare a payment.", "accountId (UUID), paymentReference, idempotencyKey, direction, amount, currency"),
        new("payments.get", true, "Read a payment by its identifier.", "paymentId (UUID)"),
        new("portfolios.create", false, "Create a client portfolio.", "clientId (UUID), name, baseCurrency"),
        new("portfolios.positions", true, "Read positions in a client portfolio.", "clientId (UUID), portfolioId (UUID)"),
        new("reconciliation.start", false, "Start a reconciliation run.", "source, runReference"),
        new("reconciliation.complete", false, "Complete a reconciliation run.", "runId (UUID)"),
        new("referenceData.isBusinessDay", true, "Check whether a date is a business day.", "calendarId (UUID), date (YYYY-MM-DD)"),
        new("referenceData.setCurrency", false, "Create or update currency reference data.", "code, name, minorUnits"),
        new("reporting.queue", false, "Queue a report.", "reportType, optional parametersJson"),
        new("reporting.audit", false, "Record an audit event.", "action, entityType, optional entityId, correlationId, payloadJson"),
        new("risk.checkLimit", true, "Check a proposed value against an account risk limit.", "accountId (UUID), limitType, proposedValue, currency"),
        new("risk.createAlert", false, "Create a risk alert.", "alertType, severity, description, optional accountId (UUID)"),
        new("settlement.createBatch", false, "Create a settlement batch.", "batchReference, settlementDate (YYYY-MM-DD)"),
        new("settlement.markSettled", false, "Mark a settlement instruction as settled.", "instructionId (UUID)"),
        new("tax.createStatement", false, "Prepare a tax statement.", "accountId (UUID), taxYear, jurisdiction, storageKey"),
        new("workflow.start", false, "Start a workflow instance.", "definitionId (UUID), subjectType, subjectId, tasks"),
        new("workflow.claimTask", false, "Claim a workflow task.", "taskId (UUID)"),
        new("workflow.decideTask", false, "Record a workflow task decision.", "taskId (UUID), approve, optional comment")
    ];

    public static ChatFlow? Find(string method) =>
        Flows.FirstOrDefault(flow => string.Equals(flow.Method, method, StringComparison.Ordinal));
}
