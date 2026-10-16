using System.Text.Json;
using System.Text.Json.Serialization;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Host.Runtime;

/// <summary>Durable Host adapter for the Kernel-owned P25 journal contract.</summary>
public sealed class FileConsultationJournal : IConsultationJournal, IDisposable
{
    private readonly string _path;
    private readonly Dictionary<ConsultationKey, ConsultationJournalEntry> _entries = new();
    private readonly object _sync = new();
    private readonly JsonSerializerOptions _json = CreateJsonOptions();
    private StreamWriter? _writer;

    public FileConsultationJournal(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Consultation journal path is required", nameof(filePath));
        _path = filePath;
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        Load();
        _writer = new StreamWriter(filePath, append: true) { AutoFlush = true };
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new FileAgentDecisionJsonConverter());
        return options;
    }

    public ConsultationJournalEntry? Find(ConsultationKey key)
    {
        lock (_sync) return _entries.GetValueOrDefault(key);
    }

    public ConsultationJournalEntry Reserve(ConsultationKey key, string requestFingerprint)
    {
        lock (_sync)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                EnsureFingerprint(existing, requestFingerprint);
                return existing;
            }
            var entry = new ConsultationJournalEntry(
                key, requestFingerprint, ConsultationJournalStatus.Pending, null, null,
                ConsumedCountCore(key.RunId, key.ContractViewRef) + 1);
            Store(entry);
            return entry;
        }
    }

    public ConsultationJournalEntry Complete(ConsultationKey key, string requestFingerprint, AgentDecision decision) =>
        Finish(key, requestFingerprint, ConsultationJournalStatus.Completed, decision, null);

    public ConsultationJournalEntry Reject(ConsultationKey key, string requestFingerprint, string reason) =>
        Finish(key, requestFingerprint, ConsultationJournalStatus.Rejected, null, reason);

    public int ConsumedCount(string runId, string contractViewRef)
    {
        lock (_sync) return ConsumedCountCore(runId, contractViewRef);
    }

    public bool HasPending(string runId, string contractViewRef)
    {
        lock (_sync)
            return _entries.Values.Any(entry => entry.Key.RunId == runId
                && entry.Key.ContractViewRef == contractViewRef
                && entry.Status == ConsultationJournalStatus.Pending);
    }

    private ConsultationJournalEntry Finish(
        ConsultationKey key, string requestFingerprint, ConsultationJournalStatus status,
        AgentDecision? decision, string? reason)
    {
        lock (_sync)
        {
            if (!_entries.TryGetValue(key, out var existing))
                throw new KeyNotFoundException($"Consultation '{key.ConsultationCorrelationId}' was not reserved");
            EnsureFingerprint(existing, requestFingerprint);
            if (existing.Status != ConsultationJournalStatus.Pending)
                return existing;
            var updated = existing with { Status = status, Decision = decision, RejectionReason = reason };
            Store(updated);
            return updated;
        }
    }

    private void Store(ConsultationJournalEntry entry)
    {
        _entries[entry.Key] = entry;
        _writer?.WriteLine(JsonSerializer.Serialize(entry, _json));
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        foreach (var line in File.ReadLines(_path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var entry = JsonSerializer.Deserialize<ConsultationJournalEntry>(line, _json);
                if (entry is not null) _entries[entry.Key] = entry;
            }
            catch (JsonException) { /* torn tail frame is not committed */ }
        }
    }

    private int ConsumedCountCore(string runId, string contractViewRef) =>
        _entries.Values.Count(entry => entry.Key.RunId == runId
            && entry.Key.ContractViewRef == contractViewRef
            && entry.Status != ConsultationJournalStatus.Pending);

    private static void EnsureFingerprint(ConsultationJournalEntry existing, string fingerprint)
    {
        if (!string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal))
            throw new ConsultationJournalConflictException(
                $"consultation-payload-conflict:{existing.Key.ConsultationCorrelationId}");
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}

/// <summary>
/// File-only representation for the Kernel journal. Agent.Dsh owns the wire
/// converter and its <c>kind</c> contract; this adapter uses a private
/// discriminator so adding durable replay does not change the shared DSH
/// protocol or its generated schema.
/// </summary>
internal sealed class FileAgentDecisionJsonConverter : JsonConverter<AgentDecision>
{
    public override AgentDecision? Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var kind = root.GetProperty("$kind").GetString();
        return kind switch
        {
            "act" => new AgentDecision.Act(root.GetProperty("proposal").Deserialize<AgentActionProposal>(options)!),
            "noAction" => new AgentDecision.NoAction(root.GetProperty("proposal").Deserialize<AgentNoActionProposal>(options)! ),
            "defer" => new AgentDecision.Defer(
                root.GetProperty("decisionId").GetString()!,
                root.GetProperty("spec").Deserialize<ObserveSpec>(options)!,
                root.TryGetProperty("renewal", out var deferRenewal)
                    ? deferRenewal.Deserialize<BudgetRenewalRequest>(options) : null),
            "policy" => new AgentDecision.Policy(
                root.GetProperty("decisionId").GetString()!,
                root.GetProperty("proposal").Deserialize<PolicyProposal>(options)! ),
            "plan" => new AgentDecision.Plan(
                root.GetProperty("decisionId").GetString()!,
                root.GetProperty("proposal").Deserialize<AgentPlanProposal>(options)! ),
            "skip" => new AgentDecision.Skip(
                root.GetProperty("decisionId").GetString()!,
                root.GetProperty("target").GetString()!,
                root.GetProperty("reason").GetString()!,
                root.TryGetProperty("evidenceRefs", out var evidenceRefs)
                    ? evidenceRefs.Deserialize<IReadOnlyList<string>>(options) ?? Array.Empty<string>()
                    : Array.Empty<string>()),
            _ => throw new JsonException($"Unknown durable AgentDecision kind '{kind}'"),
        };
    }

    public override void Write(Utf8JsonWriter writer, AgentDecision value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case AgentDecision.Act act:
                writer.WriteString("$kind", "act");
                writer.WritePropertyName("proposal");
                JsonSerializer.Serialize(writer, act.Proposal, options);
                break;
            case AgentDecision.NoAction noAction:
                writer.WriteString("$kind", "noAction");
                writer.WritePropertyName("proposal");
                JsonSerializer.Serialize(writer, noAction.Proposal, options);
                break;
            case AgentDecision.Defer defer:
                writer.WriteString("$kind", "defer");
                writer.WriteString("decisionId", defer.DecisionId);
                writer.WritePropertyName("spec");
                JsonSerializer.Serialize(writer, defer.Spec, options);
                if (defer.Renewal is not null)
                {
                    writer.WritePropertyName("renewal");
                    JsonSerializer.Serialize(writer, defer.Renewal, options);
                }
                break;
            case AgentDecision.Policy policy:
                writer.WriteString("$kind", "policy");
                writer.WriteString("decisionId", policy.DecisionId);
                writer.WritePropertyName("proposal");
                JsonSerializer.Serialize(writer, policy.Proposal, options);
                break;
            case AgentDecision.Plan plan:
                writer.WriteString("$kind", "plan");
                writer.WriteString("decisionId", plan.DecisionId);
                writer.WritePropertyName("proposal");
                JsonSerializer.Serialize(writer, plan.Proposal, options);
                break;
            case AgentDecision.Skip skip:
                writer.WriteString("$kind", "skip");
                writer.WriteString("decisionId", skip.DecisionId);
                writer.WriteString("target", skip.Target);
                writer.WriteString("reason", skip.Reason);
                writer.WritePropertyName("evidenceRefs");
                JsonSerializer.Serialize(writer, skip.EvidenceRefs, options);
                break;
            default:
                throw new JsonException($"Unsupported durable AgentDecision type {value.GetType().Name}");
        }
        writer.WriteEndObject();
    }
}
