using System.Security.Cryptography;
using System.Text.Json;

namespace UniClaw.Host.Dsh;

/// <summary>Harness diagnosis codec. Model conclusions remain non-authoritative;
/// producer and consumed input provenance are supplied by the tool adapter.</summary>
internal static class RuntimeDiagnosisPacket
{
    internal static (byte[] Bytes, string Summary) Encode(
        string response, string model, string reportPath, byte[] reportBytes, string runDir)
    {
        try
        {
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;
            Require(Text(root, "schema") == "uniclaw.diagnosis.packet.v0", "schema");
            Require(Text(root, "authority") == "non-authoritative", "authority");
            var level = Text(root, "evidenceLevel");
            Require(new[] { "E0", "E1", "E2", "E3", "E4" }.Contains(level), "evidenceLevel");
            var failureClass = Text(root, "failureClass");
            Require(new[] { "A", "B", "C", "D", "E", "F" }.Contains(failureClass), "failureClass");
            var summary = Strings(root, "evidenceSummary", nonempty: true);
            var divergence = root.GetProperty("firstDivergence");
            foreach (var key in new[] { "where", "expected", "observed" }) Text(divergence, key);
            Strings(divergence, "evidenceRefs", nonempty: true);
            var escalation = root.GetProperty("escalation");
            var required = escalation.GetProperty("required").GetBoolean();
            Strings(escalation, "decisionRequest", nonempty: required);
            if (required || escalation.TryGetProperty("reason", out _)) Text(escalation, "reason");
            var owner = Text(root, "owner");
            var packet = new Dictionary<string, object?>
            {
                ["schema"] = "uniclaw.diagnosis.packet.v0",
                ["authority"] = "non-authoritative",
                ["meta"] = new { diagnosedAt = DateTimeOffset.UtcNow.ToString("O"),
                    producer = new { kind = "model-procedure", model } },
                ["subject"] = new { runDir, inputs = new[] { new {
                    path = reportPath, sha256 = Hash(reportBytes), role = "run-report" } } },
                ["evidenceLevel"] = level, ["evidenceSummary"] = summary,
                ["failureClass"] = failureClass,
                ["lifecycleStage"] = Text(root, "lifecycleStage"),
                ["lastCorrectState"] = Text(root, "lastCorrectState"),
                ["firstDivergence"] = divergence.Clone(), ["owner"] = owner,
                ["rootCauseSupport"] = Strings(root, "rootCauseSupport"),
                ["remainingUncertainty"] = Strings(root, "remainingUncertainty"),
                ["escalation"] = escalation.Clone(),
            };
            return (JsonSerializer.SerializeToUtf8Bytes(packet, new JsonSerializerOptions { WriteIndented = true }),
                RuntimeToolHost.DiagnosisNotice + "\n\n" + string.Join("\n", summary.Select(s => "- " + s))
                + $"\n\nEvidence: {level}; class: {failureClass}; owner: {owner}"
                + $"\nExpected: {Text(divergence, "expected")}\nObserved: {Text(divergence, "observed")}");
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidOperationException("diagnosis-packet-invalid: " + error.Message, error);
        }
    }

    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string Text(JsonElement node, string key)
    {
        var value = node.GetProperty(key).GetString();
        Require(!string.IsNullOrWhiteSpace(value), key);
        return value!;
    }

    private static string[] Strings(JsonElement node, string key, bool nonempty = false)
    {
        var values = node.GetProperty(key).EnumerateArray().Select(value => value.GetString()).ToArray();
        Require((!nonempty || values.Length > 0) && values.All(value => !string.IsNullOrWhiteSpace(value)), key);
        return values.Select(value => value!).ToArray();
    }

    private static void Require(bool condition, string field)
    {
        if (!condition) throw new InvalidOperationException("invalid " + field);
    }
}
