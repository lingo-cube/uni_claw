#!/usr/bin/env dotnet run
// tools/legacy-perception-import/import.cs — PER-002 一次性 migration adapter
//（dies with migration; normalized evidence survives）。
//
// 用途：uni-agent reality-evidence 的 uiautomator hierarchy XML（+ golden-run-v1
// 真实 provider JSON）→ target-neutral corpus observations（tests 侧
// Corpus/scenarios.json）。不依赖、不修改任何 Product 代码；不解析任何 legacy
// world-truth（CurrentPage/ContainerId/goal 状态不在此数据中，也不得出现）。
//
// 运行：dotnet run tools/legacy-perception-import/import.cs -- <corpusDir>

using System.Text.Json;
using System.Xml.Linq;

var corpusDir = args.Length > 0 ? args[0]
    : throw new ArgumentException("usage: import.cs <corpusDir>");

var scenarios = new List<object>();
var captureIndex = 0;

// ---- ADAPT：uiautomator XML → observations --------------------------------
foreach (var xmlPath in Directory.GetFiles(Path.Combine(corpusDir, "artifacts"), "*.xml").OrderBy(p => p))
{
    var scenarioId = Path.GetFileNameWithoutExtension(xmlPath);
    var doc = XDocument.Load(xmlPath);
    var observations = new List<Dictionary<string, string>>();
    var keyUsage = new Dictionary<string, int>();
    var nodeIndex = 0;

    foreach (var node in doc.Descendants("node"))
    {
        var resourceId = (string?)node.Attribute("resource-id") ?? "";
        var text = (string?)node.Attribute("text") ?? "";
        var cls = (string?)node.Attribute("class") ?? "";
        var bounds = (string?)node.Attribute("bounds") ?? "";
        var clickable = (string?)node.Attribute("clickable") ?? "false";

        if (resourceId.Length == 0 && text.Length == 0)
            continue;

        // ListView 等容器会复用 resource-id：同 key 多节点按文档序加序号去重，
        // 避免同帧内制造互相冲突的同 subject observations。
        var baseKey = resourceId.Length > 0
            ? resourceId[(resourceId.LastIndexOf('/') + 1)..]
            : "node";
        var ordinal = keyUsage.TryGetValue(baseKey, out var used) ? used : 0;
        keyUsage[baseKey] = ordinal + 1;
        var key = ordinal == 0 ? baseKey : $"{baseKey}{ordinal + 1}";
        nodeIndex++;

        if (text.Length > 0)
            observations.Add(new() { ["subject"] = $"ui.text.{key}", ["value"] = text });
        if (cls.Length > 0)
            observations.Add(new() { ["subject"] = $"ui.node.{key}.class", ["value"] = cls.Split('.').Last() });
        if (bounds.Length > 0)
        {
            // uiautomator bounds "[l,t][r,b]"（pixel, artifact 坐标系）→ "l,t,r,b"
            var b = bounds.Trim('[', ']').Split("][");
            observations.Add(new() { ["subject"] = $"spatial.artifact.bounds.{key}", ["value"] = $"{b[0]},{b[1]}" });
        }
        if (clickable == "true")
            observations.Add(new() { ["subject"] = $"ui.node.{key}.clickable", ["value"] = "true" });

        // Fast 语义 hint：fixture 页面的 scenario_title 即 page signature
        //（复用 id 去重后的第一个 scenario_title 节点）
        if (resourceId.EndsWith("scenario_title") && text.Length > 0
            && observations.All(o => o["subject"] != "perception.page.signature"))
            observations.Add(new() { ["subject"] = "perception.page.signature", ["value"] = text });
    }

    scenarios.Add(new Dictionary<string, object?>
    {
        ["scenarioId"] = scenarioId,
        ["provenance"] = "legacy-adapted",
        ["source"] = $"uni-agent:tools/android-runtime-reality-fixture/reality-evidence/{LegacySourceName(scenarioId)}.xml",
        ["artifact"] = $"artifacts/{scenarioId}.png",
        ["captureTime"] = $"2026-09-09T10:{captureIndex:D2}:00+00:00",
        ["knownContext"] = new Dictionary<string, string?> { ["kind"] = "initial" },
        ["observations"] = observations,
        ["knownAmbiguity"] = null,
        ["knownFailure"] = null,
    });
    captureIndex += 2;
}

// ---- DIRECT：golden-run-v1 真实 provider JSON（YOLO + OCR）----------------
var goldenPath = Path.Combine(corpusDir, "legacy-direct", "golden-run-v1", "case-a-before.json");
if (File.Exists(goldenPath))
{
    using var doc = JsonDocument.Parse(File.ReadAllText(goldenPath));
    var root = doc.RootElement;
    var observations = new List<Dictionary<string, string>>();

    foreach (var det in root.GetProperty("yolo").EnumerateArray())
    {
        var id = det.GetProperty("id").GetString()!;
        var label = det.GetProperty("label").GetString()!;
        var px = det.GetProperty("boundsPx").EnumerateArray().Select(e => e.GetInt32()).ToArray();
        observations.Add(new() { ["subject"] = $"ui.detect.{id}.class", ["value"] = label });
        observations.Add(new() { ["subject"] = $"spatial.artifact.bounds.detect.{id}", ["value"] = $"{px[0]},{px[1]},{px[2]},{px[3]}" });
    }
    var ocrIndex = 0;
    foreach (var token in root.GetProperty("ocr").EnumerateArray())
    {
        var text = token.GetProperty("text").GetString()!;
        var px = token.GetProperty("boundsPx").EnumerateArray().Select(e => e.GetInt32()).ToArray();
        observations.Add(new() { ["subject"] = $"ui.text.ocr{ocrIndex}", ["value"] = text });
        observations.Add(new() { ["subject"] = $"spatial.artifact.bounds.ocr{ocrIndex}", ["value"] = $"{px[0]},{px[1]},{px[2]},{px[3]}" });
        ocrIndex++;
    }

    scenarios.Add(new Dictionary<string, object?>
    {
        ["scenarioId"] = "golden-case-a-before",
        ["provenance"] = "legacy-direct",
        ["source"] = "uni-agent:tests/UniClaw.Runtime.Tests/Perception/Assets/golden-run-v1/perception/case-a-before.json（真实 YOLO+RapidOCR provider 输出，emulator-5554）",
        ["artifact"] = "legacy-direct/golden-run-v1/case-a-before.png",
        ["captureTime"] = "2026-09-09T10:30:00+00:00",
        ["knownContext"] = new Dictionary<string, string?> { ["kind"] = "initial" },
        ["observations"] = observations,
        ["knownAmbiguity"] = "无 page signature（真实 OCR tokens；identity 判定留给 UIWorld）",
        ["knownFailure"] = null,
    });
}

var manifestPath = Path.Combine(corpusDir, "scenarios.json");
using (var stream = File.Create(manifestPath))
using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
{
    writer.WriteStartObject();
    writer.WriteNumber("corpusVersion", 1);
    writer.WriteString("generatedBy",
        "tools/legacy-perception-import/import.cs（one-time migration adapter；adapter dies with migration，本文件与 artifacts 为 normalized survivors）");
    writer.WritePropertyName("frameDefaults");
    writer.WriteStartObject();
    writer.WriteNumber("width", 1080);
    writer.WriteNumber("height", 1920);
    writer.WriteString("frame", "artifact");
    writer.WriteEndObject();

    writer.WritePropertyName("scenarios");
    writer.WriteStartArray();
    foreach (var s in scenarios)
    {
        var sc = (Dictionary<string, object?>)s;
        writer.WriteStartObject();
        writer.WriteString("scenarioId", (string)sc["scenarioId"]!);
        writer.WriteString("provenance", (string)sc["provenance"]!);
        writer.WriteString("source", (string)sc["source"]!);
        writer.WriteString("artifact", (string)sc["artifact"]!);
        writer.WriteString("captureTime", (string)sc["captureTime"]!);
        var ctx = (Dictionary<string, string?>)sc["knownContext"]!;
        writer.WritePropertyName("knownContext");
        writer.WriteStartObject();
        foreach (var kv in ctx) writer.WriteString(kv.Key, kv.Value);
        writer.WriteEndObject();
        writer.WritePropertyName("observations");
        writer.WriteStartArray();
        foreach (var o in (List<Dictionary<string, string>>)sc["observations"]!)
        {
            writer.WriteStartObject();
            writer.WriteString("subject", o["subject"]);
            writer.WriteString("value", o["value"]);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        if (sc["knownAmbiguity"] is string ambiguity) writer.WriteString("knownAmbiguity", ambiguity);
        else writer.WriteNull("knownAmbiguity");
        writer.WriteNull("knownFailure");
        writer.WriteEndObject();
    }
    writer.WriteEndArray();
    writer.WriteEndObject();
}
Console.WriteLine($"scenarios: {scenarios.Count} → {manifestPath}");

static string LegacySourceName(string scenarioId) => scenarioId switch
{
    "popup04-page" => "POPUP-04/dialog",
    "scroll01-v1" => "SCROLL-01/v1",
    "scroll01-v2" => "SCROLL-01/v2",
    "popup09-before" => "POPUP-01/before",
    "popup01-dialog" => "POPUP-01/popup",
    "nav03-parent" => "NAV-03/parent",
    "nav03-childa" => "NAV-03/childA",
    _ => scenarioId,
};
