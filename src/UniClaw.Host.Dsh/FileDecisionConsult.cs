using System.Text.Json;
using UniClaw.Agent.Dsh;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Host.Dsh;

/// <summary>
/// Test-only decision bridge. A leader writes one response for the decision id
/// in the inbox; the Host waits for that response and feeds the same closed
/// AgentDecision union into the normal Host/Kernel chain. This is deliberately
/// a file seam, not a DSH realization or a second decision protocol.
/// </summary>
public sealed class FileDecisionConsult
{
    public const string DecisionSource = "leader-file";
    public static readonly TimeSpan DefaultResponseTimeout = TimeSpan.FromSeconds(300);

    private readonly string _inbox;
    private readonly TimeSpan _timeout;
    private readonly JsonSerializerOptions _wire = ProductProtocolJson.CreateOptions();
    private readonly JsonSerializerOptions _plain = new(JsonSerializerDefaults.Web);
    private int _attempt;

    public FileDecisionConsult(string inboxDirectory, TimeSpan? responseTimeout = null)
    {
        if (string.IsNullOrWhiteSpace(inboxDirectory))
            throw new ArgumentException("Decision inbox is required.", nameof(inboxDirectory));
        _inbox = Path.GetFullPath(inboxDirectory);
        _timeout = responseTimeout ?? DefaultResponseTimeout;
        if (_timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(responseTimeout));
        Directory.CreateDirectory(_inbox);
        Directory.CreateDirectory(Path.Combine(_inbox, "receipts"));
    }

    /// <summary>Initialization selected by the leader response, if supplied.</summary>
    public AgentTaskInitialization? LastTaskInitialization { get; private set; }

    public AgentDecision? Consult(AgentDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var attempt = Interlocked.Increment(ref _attempt);
        var safeId = SafeFilePart(context.DecisionId);
        var requestPath = Path.Combine(_inbox, $"request-{safeId}.json");
        var responsePath = Path.Combine(_inbox, $"response-{safeId}.json");
        WriteRequest(requestPath, context, attempt);
        Console.Error.WriteLine($"decision-source={DecisionSource} decisionId={context.DecisionId} attempt={attempt}");

        var deadline = DateTimeOffset.UtcNow + _timeout;
        DateTimeOffset? rejectedWrite = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (File.Exists(responsePath))
            {
                try
                {
                    var stamp = File.GetLastWriteTimeUtc(responsePath);
                    if (rejectedWrite is null || stamp != rejectedWrite.Value.UtcDateTime)
                    {
                        var response = ReadResponse(responsePath, context.DecisionId);
                        LastTaskInitialization = response.Initialization;
                        ArchiveResponse(responsePath, safeId, attempt);
                        WriteReceipt(context, response.Decision, attempt);
                        return response.Decision;
                    }
                }
                catch (JsonException error)
                {
                    rejectedWrite = File.GetLastWriteTimeUtc(responsePath);
                    Console.Error.WriteLine($"decision-source={DecisionSource} invalid-response decisionId={context.DecisionId}: {error.Message}");
                }
                catch (IOException)
                {
                    // The leader may still be atomically replacing the file.
                }
            }
            Thread.Sleep(TimeSpan.FromMilliseconds(250));
        }

        Console.Error.WriteLine($"decision-source={DecisionSource} timeout decisionId={context.DecisionId} after={_timeout.TotalSeconds:0}s");
        return null;
    }

    private void WriteRequest(string path, AgentDecisionContext context, int attempt)
    {
        var payload = new
        {
            schemaVersion = "uniclaw.leader-file-decision-request.v1",
            decisionSource = DecisionSource,
            decisionId = context.DecisionId,
            attempt,
            createdAt = DateTimeOffset.UtcNow,
            context,
        };
        WriteAtomic(path, JsonSerializer.SerializeToUtf8Bytes(payload, _wire));
    }

    private Response ReadResponse(string path, string expectedDecisionId)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("leader response must be an object");

        var initialization = default(AgentTaskInitialization);
        JsonElement decisionElement;
        if (root.TryGetProperty("task", out var task) && task.ValueKind == JsonValueKind.Object)
        {
            if (!task.TryGetProperty("payload", out decisionElement))
                throw new JsonException("leader task response requires payload");
            if (task.TryGetProperty("initialization", out var init))
                initialization = init.Deserialize<AgentTaskInitialization>(_wire)
                    ?? throw new JsonException("leader task initialization is null");
        }
        else if (root.TryGetProperty("decision", out var wrapped))
        {
            decisionElement = wrapped;
            if (root.TryGetProperty("initialization", out var init))
                initialization = init.Deserialize<AgentTaskInitialization>(_wire)
                    ?? throw new JsonException("leader initialization is null");
        }
        else
        {
            decisionElement = root;
        }

        var decision = DeserializeDecision(decisionElement);
        var returnedId = AgentDecisionCorrelation.TryGetDecisionId(decision);
        if (!string.Equals(returnedId, expectedDecisionId, StringComparison.Ordinal))
            throw new JsonException($"leader decisionId '{returnedId}' does not match '{expectedDecisionId}'");
        return new Response(decision, initialization);
    }

    private AgentDecision DeserializeDecision(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new JsonException("leader decision must be an object");
        var kind = RequiredString(element, "kind");
        if (!string.Equals(kind, "plan", StringComparison.Ordinal))
            return JsonSerializer.Deserialize<AgentDecision>(element.GetRawText(), _wire)
                ?? throw new JsonException("leader decision is null");

        EnsureOnly(element, "kind", "decisionId", "proposal");
        var decisionId = RequiredString(element, "decisionId");
        var proposal = RequiredObject(element, "proposal");
        EnsureOnly(proposal, "items", "justification");
        var itemsElement = RequiredArray(proposal, "items");
        var items = new List<PlanItem>();
        foreach (var item in itemsElement.EnumerateArray())
            items.Add(ReadPlanItem(item));
        var justification = OptionalString(proposal, "justification");
        return new AgentDecision.Plan(decisionId, new AgentPlanProposal(items, justification));
    }

    private PlanItem ReadPlanItem(JsonElement item)
    {
        var kind = RequiredString(item, "kind");
        return kind switch
        {
            "act" => ReadActItem(item),
            "observe" => ReadObserveItem(item),
            "control" => ReadControlItem(item),
            _ => throw new JsonException($"unknown leader plan item kind '{kind}'"),
        };
    }

    private static PlanItem ReadActItem(JsonElement item)
    {
        EnsureOnly(item, "kind", "targetRole", "targetDescriptor", "effectClass", "desiredState");
        return new PlanItem.ActItem(
            RequiredString(item, "targetRole"), OptionalString(item, "targetDescriptor"),
            RequiredString(item, "effectClass"), OptionalString(item, "desiredState"));
    }

    private static PlanItem ReadObserveItem(JsonElement item)
    {
        EnsureOnly(item, "kind", "subject");
        return new PlanItem.ObserveItem(OptionalString(item, "subject"));
    }

    private static PlanItem ReadControlItem(JsonElement item)
    {
        EnsureOnly(item, "kind", "control", "controlKind", "reason");
        var control = item.TryGetProperty("control", out var legacy)
            ? legacy.GetString()
            : item.TryGetProperty("controlKind", out var current) ? current.GetString() : null;
        if (!Enum.TryParse<PlanControlKind>(control, ignoreCase: true, out var parsed))
            throw new JsonException("leader control item requires reobserve, replan, or stop");
        return new PlanItem.ControlItem(parsed, OptionalString(item, "reason"));
    }

    private void WriteReceipt(AgentDecisionContext context, AgentDecision decision, int attempt)
    {
        var kind = decision switch
        {
            AgentDecision.Act => "act",
            AgentDecision.Plan => "plan",
            AgentDecision.Defer => "defer",
            AgentDecision.NoAction => "noAction",
            AgentDecision.Policy => "policy",
            AgentDecision.Skip => "skip",
            _ => "unknown",
        };
        var receipt = new
        {
            schemaVersion = "uniclaw.leader-file-decision-receipt.v1",
            decisionSource = DecisionSource,
            decisionId = context.DecisionId,
            kind,
            attempt,
            receivedAt = DateTimeOffset.UtcNow,
        };
        var path = Path.Combine(_inbox, "receipts", $"decision-{SafeFilePart(context.DecisionId)}-{attempt:0000}.json");
        WriteAtomic(path, JsonSerializer.SerializeToUtf8Bytes(receipt, _plain));
    }

    private void ArchiveResponse(string path, string safeId, int attempt)
    {
        var archived = Path.Combine(_inbox, $"response-{safeId}-consumed-{attempt:0000}.json");
        try { File.Move(path, archived, overwrite: true); }
        catch (IOException) { /* leave the response in place; the decision is already accepted */ }
    }

    private static void WriteAtomic(string path, byte[] payload)
    {
        var temporary = path + $".tmp-{Guid.NewGuid():N}";
        File.WriteAllBytes(temporary, payload);
        File.Move(temporary, path, overwrite: true);
    }

    private static string SafeFilePart(string value) =>
        string.Concat(value.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '_'));

    private static JsonElement RequiredObject(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Object
            ? value : throw new JsonException($"leader property '{property}' must be an object");

    private static JsonElement RequiredArray(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value : throw new JsonException($"leader property '{property}' must be an array");

    private static string RequiredString(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new JsonException($"leader property '{property}' must be a non-empty string");

    private static string? OptionalString(JsonElement root, string property) =>
        !root.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null
            ? null
            : value.ValueKind == JsonValueKind.String ? value.GetString() : throw new JsonException($"leader property '{property}' must be a string or null");

    private static void EnsureOnly(JsonElement root, params string[] allowed)
    {
        var permitted = allowed.ToHashSet(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
            if (!permitted.Contains(property.Name))
                throw new JsonException($"unexpected leader property '{property.Name}'");
    }

    private sealed record Response(AgentDecision Decision, AgentTaskInitialization? Initialization);
}
