using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Host.Tests;

/// <summary>
/// Rebuilds the supported v1 on-disk transport for domain corruption tests.
/// These fixtures keep semantic rejection assertions independent of the current writer version.
/// </summary>
internal static class CoreHistoryLegacyFixture
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void WriteControl(string path, string recordingId, IReadOnlyList<ControlTraversalHistoryRecord> records,
        Action<JsonArray> mutate)
    {
        var rows = new JsonArray(records.Select(record => JsonSerializer.SerializeToNode(record, Json)).ToArray());
        mutate(rows);
        Write(path, "uniclaw.control.traversal.history.v1", "uniclaw.control.traversal.history-head.v1",
            recordingId, rows.Select(row => row!).ToArray());
    }

    public static void WriteRuntimePlan(string path, string recordingId, IReadOnlyList<RuntimePlanHistoryRecord> records,
        Action<JsonArray> mutate)
    {
        var rows = new JsonArray(records.Select(record => JsonSerializer.SerializeToNode(record, Json)).ToArray());
        mutate(rows);
        Write(path, "uniclaw.runtime.plan.history.v1", "uniclaw.runtime.plan.history-head.v1",
            recordingId, rows.Select(row => row!).ToArray());
    }

    private static void Write(string path, string schema, string headSchema, string recordingId,
        IReadOnlyList<JsonNode> rows)
    {
        var frames = new List<string>(rows.Count);
        var previous = "";
        foreach (var row in rows)
        {
            var payload = row.ToJsonString(Json);
            var hash = Hash(previous + "\n" + recordingId + "\n" + payload);
            frames.Add(JsonSerializer.Serialize(new { schema, recordingId, previous, payload, hash }, Json));
            previous = hash;
        }

        File.WriteAllText(path, string.Join("\n", frames) + "\n");
        File.WriteAllText(path + ".head", JsonSerializer.Serialize(new
        {
            schema = headSchema,
            recordingId,
            sequence = rows.Count,
            hash = previous,
        }, Json) + "\n");
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
