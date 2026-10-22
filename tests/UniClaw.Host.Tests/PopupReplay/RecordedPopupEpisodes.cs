using System.Security.Cryptography;
using System.Text.Json;
using UniClaw.Host;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Simulation.Tests.ComponentReplay;

namespace UniClaw.Host.Tests.PopupReplay;

internal sealed record PopupSourceFrame(string CaptureId, string Xml, int Width, int Height, string Role,
    string RecordedPopup, string XmlSha256, string XmlPath, string SourceCycleId, string SourceContext, int TraceIndex);
internal sealed record RecordedPopupEpisode(string Id, PopupSourceFrame[] Frames, JsonElement Trigger,
    bool RecordedClearance, string[] Missing, IReadOnlyDictionary<string, string> SourceHashes);

internal static class RecordedPopupEpisodes
{
    internal const string RelativeRoot = "tests/UniClaw.Host.Tests/Fixtures/PopupReplay/";
    internal static string Root
    {
        get
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md")) && File.Exists(Path.Combine(dir.FullName, "UniClaw.Kernel.slnx"))) return dir.FullName;
            throw new InvalidOperationException("Replay repository root missing");
        }
    }
    internal static string Fixture(string name) => Path.Combine(Root, RelativeRoot, name);
    internal static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    internal static ReplayCompositionGuard Guard() => ReplayCompositionGuard.FromJson(File.ReadAllText(Fixture("composition.json")));
    internal static IEnumerable<object[]> Cases()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Fixture("sources.json")));
        foreach (var episode in doc.RootElement.GetProperty("episodes").EnumerateArray()) yield return [episode.GetProperty("id").GetString()!];
    }

    internal static RecordedPopupEpisode Extract(string id, bool requireRecordedClearance = false)
    {
        using var source = JsonDocument.Parse(File.ReadAllText(Fixture("sources.json")));
        if (source.RootElement.GetProperty("schemaVersion").GetString() != "recorded-popup-source.v1") throw new InvalidDataException("unsupported-popup-source");
        var episode = source.RootElement.GetProperty("episodes").EnumerateArray().Single(e => e.GetProperty("id").GetString() == id);
        var hashes = new Dictionary<string, string>();
        byte[] Read(JsonElement asset)
        {
            var path = asset.GetProperty("path").GetString()!; var expected = asset.GetProperty("sha256").GetString();
            var bytes = File.ReadAllBytes(Path.Combine(Root, path)); var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (hash != expected) throw new InvalidDataException("popup-source-hash-mismatch:" + path);
            hashes.Add(path, hash); return bytes;
        }
        using var trace = JsonDocument.Parse(Read(episode.GetProperty("trace")));
        using var steps = JsonDocument.Parse(Read(episode.GetProperty("steps")));
        var frames = new List<PopupSourceFrame>(); var previousIndex = -1;
        foreach (var spec in episode.GetProperty("frames").EnumerateArray())
        {
            var index = spec.GetProperty("traceIndex").GetInt32(); var capture = spec.GetProperty("captureId").GetString()!;
            if (index <= previousIndex || trace.RootElement[index].GetProperty("captureId").GetString() != capture)
                throw new InvalidDataException("popup-capture-order-or-correlation");
            previousIndex = index;
            var xmlAsset = spec.GetProperty("xml");
            if (Path.GetFileNameWithoutExtension(xmlAsset.GetProperty("path").GetString()) != capture
                || Path.GetFileNameWithoutExtension(spec.GetProperty("png").GetProperty("path").GetString()) != capture)
                throw new InvalidDataException("popup-capture-file-binding");
            var xml = System.Text.Encoding.UTF8.GetString(Read(xmlAsset));
            var png = Read(spec.GetProperty("png"));
            var width = spec.GetProperty("width").GetInt32(); var height = spec.GetProperty("height").GetInt32();
            if (png.Length < 24 || System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)) != width
                || System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)) != height)
                throw new InvalidDataException("popup-source-viewport");
            frames.Add(new(capture, xml, width, height, spec.GetProperty("role").GetString()!,
                trace.RootElement[index].GetProperty("popup").GetString()!, xmlAsset.GetProperty("sha256").GetString()!, xmlAsset.GetProperty("path").GetString()!,
                trace.RootElement[index].GetProperty("observationCycleId").GetString()!,
                trace.RootElement[index].GetProperty("context").GetString()!, index));
        }
        var trigger = steps.RootElement[episode.GetProperty("triggerStepIndex").GetInt32()];
        if (frames.Count < 2 || trigger.GetProperty("postCaptureId").GetString() != frames[1].CaptureId)
            throw new InvalidDataException("popup-trigger-capture-mismatch");
        var recordedClearance = episode.GetProperty("recordedClearance").GetBoolean();
        // v1 describes captured prefixes only. A metadata flag cannot invent
        // a missing CANCEL receipt or its post-observation.
        if (recordedClearance || requireRecordedClearance) throw new InvalidDataException("popup-clearance-not-recorded");
        return new(id, frames.ToArray(), trigger.Clone(), recordedClearance,
            episode.GetProperty("missing").EnumerateArray().Select(e => e.GetString()!).ToArray(), hashes);
    }

    internal static string? Classify(string? xml) => SettingsPopupClassifier.Classify(xml,
        "com.android.settings", PopupClearanceConfig.DefaultPopupResourceIds);

    internal static void Export(string name, object value)
    {
        if (Environment.GetEnvironmentVariable("UNICLAW_REGRESSION_REPLAY_OUTPUT") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "popup-" + name + ".json"), JsonSerializer.Serialize(value, Json));
    }
}

internal sealed record PopupInjectionReceipt(string Kind, string SourceCaptureId, string SourceSha256, string Delivery);
internal sealed class PopupReplayInjection(string kind)
{
    private bool _dropped;
    private readonly List<PopupInjectionReceipt> _receipts = [];
    internal IReadOnlyList<PopupInjectionReceipt> Receipts => _receipts.AsReadOnly();
    internal PopupSourceFrame SelectFrame(PopupSourceFrame normal, PopupSourceFrame popup, bool afterDelivery)
    {
        if (kind != "hold-after-delivery" || !afterDelivery) return normal;
        _receipts.Add(new(kind, normal.CaptureId, normal.XmlSha256,
            "replaced with recorded popup " + popup.CaptureId + " sha256:" + popup.XmlSha256));
        return popup;
    }
    internal string? Deliver(PopupSourceFrame frame)
    {
        if (kind == "drop-once" && !_dropped)
        {
            _dropped = true; _receipts.Add(new(kind, frame.CaptureId, frame.XmlSha256, "missing XML; no popup assertion")); return null;
        }
        return frame.Xml;
    }
}
