using System.Text;
using System.Text.Json;

namespace UniClaw.Kernel.Perception;

/// <summary>
/// LiveVisionStrategy — Fast Perception 的 live provider realization
/// （PER-005 ②；ADAPT legacy LocalVisionPerceptionSource 的响应解析半部，
/// transport 已上移至 VisionServiceClient）。输入 artifact 的 payload =
/// 服务响应 JSON（D9 双 artifact：响应才是 strategy 的确定性输入，锚它
/// 保证 replay 稳定）；解析契约 = tools/legacy-perception-import/import.cs
/// 的 DIRECT 映射（P-1 replay parity 锚，逐字段镜像）：
/// <code>
/// yolo[] → ui.detect.{id}.class = label
///        + spatial.artifact.bounds.detect.{id} = "{x1},{y1},{x2},{y2}"（boundsPx）
/// ocr[]  → ui.text.ocr{i} = text（帧内顺序索引，非 token id——corpus 约定）
///        + spatial.artifact.bounds.ocr{i} = boundsPx
/// </code>
/// 空 yolo/ocr（OK_EMPTY）→ 零 observation（missing detection 不产
/// observation，§18）；confidence 不进 payload（§19，无 buyer）。确定性：
/// 同 artifact → 同 observations（逐字节稳定，FCR-001 cache 前提）。
/// </summary>
public sealed class LiveVisionStrategy : IVersionedFastPerceptionStrategy
{
    /// <summary>实现族标识（跨 run / replay 稳定）。</summary>
    public string StrategyIdentity => "perception.live.vision";

    /// <summary>解析契约版本：subject 映射或 envelope 语义变化时必须变化
    /// （否则 FCR-001 缓存跨语义错误复用）。当前 = import.cs DIRECT 映射 v1。</summary>
    public string StrategyVersion => "1";

    public IReadOnlyList<ArtifactObservation> Observe(RawArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        string json;
        try
        {
            json = Encoding.UTF8.GetString(artifact.Payload);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                "live vision artifact payload 不是合法 UTF-8 JSON（transport 应已分类，fail-closed）", exception);
        }

        using var document = ParseDeterministic(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("yolo", out var yolo) || yolo.ValueKind != JsonValueKind.Array
            || !root.TryGetProperty("ocr", out var ocr) || ocr.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                "live vision artifact 缺 yolo/ocr 数组（transport 应已分类为 SchemaFailure，fail-closed）");

        var observations = new List<ArtifactObservation>(
            (yolo.GetArrayLength() + ocr.GetArrayLength()) * 2);
        foreach (var detection in yolo.EnumerateArray())
        {
            var id = detection.GetProperty("id").GetString()
                ?? throw MalformedEntry("yolo", "id 缺失");
            var label = detection.GetProperty("label").GetString()
                ?? throw MalformedEntry("yolo", "label 缺失");
            var bounds = PixelBounds(detection, "yolo", id);
            observations.Add(new ArtifactObservation($"ui.detect.{id}.class", label));
            observations.Add(new ArtifactObservation(
                $"spatial.artifact.bounds.detect.{id}", bounds));
        }

        var index = 0;
        foreach (var token in ocr.EnumerateArray())
        {
            var text = token.GetProperty("text").GetString()
                ?? throw MalformedEntry("ocr", "text 缺失");
            var bounds = PixelBounds(token, "ocr", $"ocr{index}");
            observations.Add(new ArtifactObservation($"ui.text.ocr{index}", text));
            observations.Add(new ArtifactObservation($"spatial.artifact.bounds.ocr{index}", bounds));
            index++;
        }
        return observations;
    }

    private static string PixelBounds(JsonElement entry, string family, string id)
    {
        if (!entry.TryGetProperty("boundsPx", out var pixels) || pixels.ValueKind != JsonValueKind.Array
            || pixels.GetArrayLength() != 4)
            throw MalformedEntry(family, $"{id} boundsPx 缺失/非 4 元数组");
        var values = pixels.EnumerateArray().Select(e => e.GetInt32()).ToArray();
        return $"{values[0]},{values[1]},{values[2]},{values[3]}";
    }

    private static JsonDocument ParseDeterministic(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "live vision artifact payload 不是合法 JSON（transport 应已分类，fail-closed）", exception);
        }
    }

    private static InvalidOperationException MalformedEntry(string family, string reason) =>
        new($"live vision 响应条目不合契约（{family}: {reason}）——fail-closed，不猜测");
}
