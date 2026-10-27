using System.Text.RegularExpressions;

namespace Brokerage.Host.Services;

/// <summary>Describes a generated form field for a registered workflow.</summary>
public sealed record WorkflowParameter(
    string Name,
    string Type,
    bool Required,
    IReadOnlyList<string> Options);

public static partial class WorkflowParameterCatalog
{
    private static readonly IReadOnlyDictionary<(string Method, string Name), string[]> SelectOptions =
        new Dictionary<(string Method, string Name), string[]>
        {
            [("accounts.createJournal", "type")] = ["Debit", "Credit"],
            [("admin.setOperationalControl", "isEnabled")] = ["true", "false"],
            [("clients.reviewKyc", "decision")] = ["Pending", "InReview", "Approved", "Rejected"],
            [("integrations.enqueue", "direction")] = ["Inbound", "Outbound"],
            [("orders.place", "side")] = ["Buy", "Sell"],
            [("orders.place", "type")] = ["Market", "Limit", "Stop", "StopLimit"],
            [("orders.place", "timeInForce")] = ["Day", "GoodTillCanceled", "ImmediateOrCancel", "FillOrKill"],
            [("payments.create", "direction")] = ["Deposit", "Withdrawal"],
            [("workflow.decideTask", "approve")] = ["true", "false"]
        };

    public static IReadOnlyList<WorkflowParameter> GetParameters(ChatFlow flow)
    {
        if (string.Equals(flow.Parameters, "no parameters", StringComparison.Ordinal))
            return [];

        var result = new List<WorkflowParameter>();
        foreach (var segment in flow.Parameters.Split(", ", StringSplitOptions.RemoveEmptyEntries))
        {
            var required = !segment.StartsWith("optional ", StringComparison.Ordinal);
            var normalizedSegment = segment.StartsWith("optional ", StringComparison.Ordinal)
                ? segment["optional ".Length..]
                : segment;

            foreach (var nameMatch in ParameterNameRegex().Matches(normalizedSegment).Cast<Match>())
            {
                var name = nameMatch.Groups["name"].Value;
                if (name == "and") continue;
                var annotation = nameMatch.Groups["annotation"].Value;
                var options = SelectOptions.GetValueOrDefault((flow.Method, name)) ?? [];
                var type = InferType(name, annotation, options);
                result.Add(new WorkflowParameter(name, type, required, options));
            }
        }

        return result;
    }

    private static string InferType(string name, string annotation, IReadOnlyList<string> options)
    {
        if (options.Count > 0) return "select";
        if (annotation == "UUID") return "uuid";
        if (annotation == "YYYY-MM-DD") return "date";
        if (annotation == "ISO 8601 timestamp") return "datetime";
        if (name.Equals("email", StringComparison.OrdinalIgnoreCase)) return "email";
        if (name.EndsWith("Json", StringComparison.Ordinal)) return "json-string";
        if (name is "entries" or "tasks") return "json";
        if (name is "bid" or "ask" or "notional" or "amount" or "quantity" or "limitPrice" or "proposedValue")
            return "number";
        if (name is "sizeBytes" or "minorUnits" or "taxYear" or "limit")
            return "integer";
        return "text";
    }

    [GeneratedRegex(@"(?<name>[A-Za-z][A-Za-z0-9]*)(?: \((?<annotation>[^)]+)\))?")]
    private static partial Regex ParameterNameRegex();
}
