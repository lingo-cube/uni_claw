using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Host.Runtime;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Host.Tests;

/// <summary>Builds a genuine v3 physical fixture for tests that mutate legacy frame bytes.</summary>
internal static class LegacyResourceFixture
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void ConvertToV3(string path)
    {
        var originalFrames = File.ReadAllLines(path).Select(line => JsonNode.Parse(line)!).ToArray();
        if (originalFrames.Length == 0) return;
        if (originalFrames[0]["schema"]?.GetValue<string>() == "uniclaw.runtime.resource.v3") return;

        RunResourceRecord[] records;
        string recordingId;
        using (var journal = new FileRunResourceJournal(path, readOnly: true))
        {
            records = journal.ReadRecords().ToArray();
            recordingId = journal.RecordingId ?? throw new InvalidDataException("resource-recording-id-missing");
        }
        if (records.Length != originalFrames.Length) throw new InvalidDataException("resource-frame-count-mismatch");

        var output = new List<string>(records.Length);
        var previous = "";
        for (var i = 0; i < records.Length; i++)
        {
            var original = originalFrames[i];
            var frameRecordingId = original["recordingId"]?.GetValue<string>();
            if (!string.Equals(frameRecordingId, recordingId, StringComparison.Ordinal))
                throw new InvalidDataException("resource-recording-id-mismatch");
            var sources = original["sources"]?.DeepClone();
            var payload = JsonSerializer.Serialize(records[i], Json);
            var serializedSources = sources is null ? "null" : sources.ToJsonString(Json);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                previous + "\n" + payload + "\n" + serializedSources + "\n" + recordingId)));
            var frame = new JsonObject
            {
                ["schema"] = "uniclaw.runtime.resource.v3",
                ["previous"] = previous,
                ["payload"] = payload,
                ["sources"] = sources,
                ["hash"] = hash,
                ["recordingId"] = recordingId,
            };
            output.Add(frame.ToJsonString(Json));
            previous = hash;
        }

        File.WriteAllText(path, string.Join("\n", output) + "\n");
        var head = new JsonObject
        {
            ["schema"] = "uniclaw.runtime.resource-head.v1",
            ["recordingId"] = recordingId,
            ["sequence"] = records.Length,
            ["hash"] = previous,
        };
        File.WriteAllText(path + ".head", head.ToJsonString(Json) + "\n");
    }
}
