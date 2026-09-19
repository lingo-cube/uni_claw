using System.Text;
using System.Text.Json;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;

namespace UniClaw.Simulation.Tests;

// ============================================================================
// UAP-001 — Phase 4 统一异步 Perception tracer 的录制输入 fixtures。
//
// 真值源（只读，不回写）：
//   - type-truth.json   ：FSV-001 Human-reviewed Ground Truth（页面元素
//     text/type/cy/clickable/id）。测试参照，不是 Evidence/Belief。
//   - type-dual-local.json：FSV-001 probe 的真实模型历史预测（truth vs
//     pred）。作为录制错误臂输入；不重算模型、不报告质量结论。
//
// UAP-001 D5 typing→role 映射（四类真值分类不合并）：
//   row_title→menu.row | row_subtitle→menu.subtitle |
//   static_title→menu.static | section_label→menu.section
// grounding 只消费 menu.row；其余 role 不匹配任何 target role——
// 「副标题/分组标题不形成第二个可点击目标」的执行面。
// ============================================================================

/// <summary>truth 元素的规范化视图（type-truth.json 一行）。</summary>
internal sealed record TruthElement(
    string Page,
    int Index,
    string Text,
    string TruthType,
    double Cy,
    bool Clickable,
    string RawId);

/// <summary>dual 预测行（type-dual-local.json 一行：page/target/truth/pred）。</summary>
internal sealed record DualPrediction(string Page, string Target, string Truth, string Pred);

/// <summary>真值分类差异核对结果（场景 ⑧-0 断言面）。</summary>
internal sealed record TruthClassificationFacts(
    IReadOnlyList<TruthElement> Truth,
    IReadOnlyList<DualPrediction> Predictions);

/// <summary>truth/fixtures 加载器（fail closed：缺文件/坏 JSON/未知分类 → 异常）。</summary>
internal static class AsyncPerceptionTruth
{
    internal const string TruthTypeRowTitle = "row_title";
    internal const string TruthTypeRowSubtitle = "row_subtitle";
    internal const string TruthTypeStaticTitle = "static_title";
    internal const string TruthTypeSectionLabel = "section_label";

    private const string TruthRelativePath =
        "platforms/perception/evaluation/reports/fsv001/screenvlm-probe/vlm-compare/type-truth.json";
    private const string DualRelativePath =
        "platforms/perception/evaluation/reports/fsv001/screenvlm-probe/vlm-compare/dual/type-dual-local.json";

    /// <summary>truth type → occurrence role（D5；未知 type fail closed）。</summary>
    internal static string RoleForType(string truthType) => truthType switch
    {
        TruthTypeRowTitle => "menu.row",
        TruthTypeRowSubtitle => "menu.subtitle",
        TruthTypeStaticTitle => "menu.static",
        TruthTypeSectionLabel => "menu.section",
        _ => throw new InvalidOperationException($"未知 truth 分类（拒绝暗中合并）: {truthType}"),
    };

    /// <summary>加载两份真值/预测文件（fail closed）。</summary>
    internal static TruthClassificationFacts Load()
    {
        var root = GoldenPaths.RepoRoot();
        var truthPath = Path.Combine(root, TruthRelativePath);
        var dualPath = Path.Combine(root, DualRelativePath);
        if (!File.Exists(truthPath))
            throw new FileNotFoundException($"truth 文件缺失: {truthPath}");
        if (!File.Exists(dualPath))
            throw new FileNotFoundException($"dual 预测文件缺失: {dualPath}");

        var truth = new List<TruthElement>();
        using (var document = JsonDocument.Parse(File.ReadAllText(truthPath)))
        {
            foreach (var page in document.RootElement.EnumerateObject())
            {
                var index = 0;
                foreach (var entry in page.Value.EnumerateArray())
                {
                    var text = entry.GetProperty("text").GetString()!;
                    var type = entry.GetProperty("type").GetString()!;
                    _ = RoleForType(type); // 未知分类 fail closed（加载期）
                    truth.Add(new TruthElement(
                        page.Name, index++, text, type,
                        entry.GetProperty("cy").GetDouble(),
                        entry.GetProperty("clickable").GetBoolean(),
                        entry.GetProperty("id").GetString() ?? ""));
                }
            }
        }

        var predictions = JsonSerializer.Deserialize<List<JsonDualRow>>(
            File.ReadAllText(dualPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("dual 预测文件反序列化为空");
        if (predictions.Any(r => r.Page is null || r.Target is null || r.Truth is null || r.Pred is null))
            throw new InvalidOperationException("dual 预测行字段缺失（page/target/truth/pred）");
        return new TruthClassificationFacts(
            truth,
            predictions.Select(r => new DualPrediction(r.Page!, r.Target!, r.Truth!, r.Pred!)).ToList());
    }

    private sealed record JsonDualRow(string? Page, string? Target, string? Truth, string? Pred);

    // ---- 页面元素 → 录制观察输入 -----------------------------------------

    /// <summary>
    /// 一条菜单元素观察（element test identity = page:index，稳定、确定性；
    /// 不提升为生产 LogicalItem identity）。
    /// </summary>
    internal sealed record ElementSpec(
        string ElementId,       // "el-{page}-{index}"
        string Page,
        string Text,
        double Cy,
        string TruthType);

    internal static string ElementIdFor(string page, int index) => $"el-{page}-{index}";

    /// <summary>某页面全部元素（按 truth 顺序）。</summary>
    internal static IReadOnlyList<ElementSpec> ElementsOfPage(
        TruthClassificationFacts facts, string page) =>
        facts.Truth
            .Where(e => e.Page == page)
            .Select(e => new ElementSpec(ElementIdFor(page, e.Index), e.Page, e.Text, e.Cy, e.TruthType))
            .ToList();

    /// <summary>预测 typing（dual 文件按 page+text 查询；缺行 → 用 truth 兜底并标注）。</summary>
    internal static string PredictedTypeFor(
        TruthClassificationFacts facts, string page, string text, string truthType)
    {
        var row = facts.Predictions.FirstOrDefault(p => p.Page == page && p.Target == text);
        return row?.Pred ?? truthType;
    }

    /// <summary>
    /// 真值坐标空间页高（px，确定性常量）：truth cy 是整页滚动坐标（最大
    /// 2335.5 > 视口 1920），归一化必须以页高为基——否则 SpatialLocator
    /// 的 [0,1] 契约被破坏。
    /// </summary>
    internal const double TruthPageHeight = 2400.0;

    /// <summary>truth cy → 像素 bounds（1080 宽；行高 60px，确定性派生）。</summary>
    internal static int[] BoundsPxFor(double cy) =>
        [0, (int)Math.Round(cy - 30.0), 1080, (int)Math.Round(cy + 30.0)];

    /// <summary>truth cy → 归一化 bounds（frame = device-viewport 归一化空间；y 以页高为基并夹紧 [0,1]）。</summary>
    internal static double[] NormalizedBoundsFor(double cy) =>
    [
        0.0,
        Math.Clamp(Math.Round((cy - 30.0) / TruthPageHeight, 6, MidpointRounding.AwayFromZero), 0.0, 1.0),
        1.0,
        Math.Clamp(Math.Round((cy + 30.0) / TruthPageHeight, 6, MidpointRounding.AwayFromZero), 0.0, 1.0),
    ];

    /// <summary>归一化 bounds 的中心 y（≈ cy/页高；断言 effect 落真实行）。</summary>
    internal static double CenterYOf(double cy) =>
        Math.Round(cy / TruthPageHeight, 6, MidpointRounding.AwayFromZero);

    // ---- provider response 合成（真 FastPerception + LiveVisionStrategy 输入）----

    /// <summary>
    /// 由（元素 × 该结果判定的 typing）合成 live provider response JSON——
    /// yolo[].label = 判定的 truth type（模型分类输出即 label）。此 bytes
    /// 经 RawArtifact.Capture 构成 capture identity（内容寻址）；真实
    /// LiveVisionStrategy 解析（UAP-001 D8：live one-shot/fast 策略代码路径）。
    /// </summary>
    internal static byte[] BuildProviderResponse(
        IEnumerable<(ElementSpec Element, string AssumedType)> typedElements)
    {
        var entries = new List<string>();
        foreach (var (element, type) in typedElements)
        {
            var bounds = BoundsPxFor(element.Cy);
            entries.Add($"{{\"id\":\"{element.ElementId}\",\"label\":\"{type}\","
                + $"\"boundsPx\":[{bounds[0]},{bounds[1]},{bounds[2]},{bounds[3]}]}}");
        }
        return Encoding.UTF8.GetBytes($"{{\"yolo\":[{string.Join(",", entries)}],\"ocr\":[]}}");
    }

    /// <summary>
    /// join：真 FastPerception 观察输出（ui.detect.*.class + spatial）× 元素表
    /// → live.frame claim value + typing claims（RFS-001 ScenarioPerceptionAdapter
    /// 同款 glue 语义：detection 按 element id 归属）。该结果未覆盖的元素
    /// 零声明（omission ≠ absence）。
    /// </summary>
    internal static (string FrameValue, IReadOnlyList<(string Subject, string Value)> TypingClaims)
        JoinDetections(
            IReadOnlyList<ObservationProposal> proposals,
            IReadOnlyList<ElementSpec> elements,
            IReadOnlyDictionary<string, string?>? elementStates = null)
    {
        var classes = new Dictionary<string, string>(StringComparer.Ordinal);
        var boundsById = new Dictionary<string, int[]>(StringComparer.Ordinal);
        foreach (var proposal in proposals)
        {
            var subject = proposal.Claim.Subject;
            if (subject.StartsWith("ui.detect.", StringComparison.Ordinal)
                && subject.EndsWith(".class", StringComparison.Ordinal))
                classes[subject["ui.detect.".Length..^".class".Length]] = proposal.Claim.Value;
            else if (subject.StartsWith("spatial.artifact.bounds.detect.", StringComparison.Ordinal))
                boundsById[subject["spatial.artifact.bounds.detect.".Length..]] =
                    proposal.Claim.Value.Split(',').Select(int.Parse).ToArray();
        }

        var joined = new List<string>();
        var typings = new List<(string, string)>();
        var matched = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in elements)
        {
            if (!classes.TryGetValue(element.ElementId, out var detectedType)
                || !boundsById.TryGetValue(element.ElementId, out _))
                continue; // 该结果未覆盖此元素 → 零声明（omission ≠ absence）
            matched.Add(element.ElementId);
            var bounds = NormalizedBoundsFor(element.Cy);
            var descriptor = JsonEscape(element.Text);
            var state = elementStates is not null
                && elementStates.TryGetValue(element.ElementId, out var elementState)
                && elementState is not null
                ? $",\"st\":\"{elementState}\""
                : "";
            joined.Add($"{{\"id\":\"{element.ElementId}\",\"cls\":\"{detectedType}\","
                + $"\"role\":\"{RoleForType(detectedType)}\",\"text\":\"{descriptor}\"{state},"
                + $"\"b\":[{bounds[0]},{bounds[1]},{bounds[2]},{bounds[3]}]}}");
            typings.Add(($"ui.typing.{element.ElementId}", detectedType));
        }
        foreach (var unmatched in classes.Keys.Where(id => !matched.Contains(id)).OrderBy(id => id, StringComparer.Ordinal))
            throw new InvalidOperationException(
                $"detection 无法关联元素表（元素表缺失 {unmatched}）——fixture 构造错误");
        return ($"{{\"detects\":[{string.Join(",", joined)}]}}", typings);
    }

    internal static string JsonEscape(string value) => value
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"");
}
