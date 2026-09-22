using System.Text.Json;

namespace UniClaw.Kernel.World;

/// <summary>
/// (源×类别)→A/B/C 信任等级表（PER-009 D5 / ADR-0028）。
/// CSS 级联查找：包名/系统特例 &gt; (producer,category) &gt; producer 默认 &gt; C。
/// 等级挂组合、不挂单条证据；每级对应采信动作：
///   A = 孤证即可授权常规动作；B = 不可逆动作前须补佐证；C = 只算线索，触发补看。
/// 初始表为冻结值（grill #16/#18 落定）；修订走 change 或自动调优（留痕可回滚）。
/// FromJson 载入（配置文件形态就绪）。
/// internal：不进公开驱动面；跨程序集消费出现时经授权提升。
/// </summary>
internal static class ProducerTrust
{
    public enum Grade { A, B, C }

    public sealed record Entry(string Producer, string? Category, Grade Value);

    public sealed record Override(string Package, string Producer, string Category, Grade Value);

    public sealed record TrustTable(
        IReadOnlyList<Entry> Grades,
        IReadOnlyList<Override> PackageOverrides)
    {
        /// <summary>级联查找（最特异者胜）；无命中 → C（fail-safe）。</summary>
        public Grade Lookup(string producer, string category, string? package = null)
        {
            if (package is { Length: > 0 })
            {
                var po = PackageOverrides.FirstOrDefault(o =>
                    o.Package == package && o.Producer == producer && o.Category == category);
                if (po is { } hit)
                    return hit.Value;
            }
            var exact = Grades.FirstOrDefault(g =>
                g.Producer == producer && g.Category == category);
            if (exact is { } e)
                return e.Value;
            var byProducer = Grades.FirstOrDefault(g =>
                g.Producer == producer && g.Category is null);
            if (byProducer is { } p)
                return p.Value;
            return Grade.C;
        }
    }

    /// <summary>producer 常量（与 Host 侧 producer 字符串保持一致）。</summary>
    public const string XmlProducer = "platform.uiautomator";
    public const string VisionProducer = "perception.live.vision";
    public const string DeepProducer = "perception.deep.vlm";

    /// <summary>冻结初始表（2026-09-22，台账 #16/#18）。</summary>
    public static TrustTable Default() => new(
        new[]
        {
            // XML：官方语义字段原生权威（api35 实发布尔；三方 app 由 B 级起步，
            // 经冲突统计可升——当前以包名覆盖降级已知不可信方）
            new Entry(XmlProducer, "state", Grade.A),
            new Entry(XmlProducer, "text", Grade.A),
            new Entry(XmlProducer, "identity", Grade.A),
            new Entry(XmlProducer, "bounds", Grade.B),   // 空间证据：定位/裁剪，非像素真相
            // 快视：结构/文本可佐证；状态是像素推断（C 级线索）
            new Entry(VisionProducer, "text", Grade.B),
            new Entry(VisionProducer, "pixel", Grade.B),
            new Entry(VisionProducer, "state", Grade.C),
            // 深模型（Tier 2 后置）：像素语义主力，仍有幻觉可能（B）
            new Entry(DeepProducer, "pixel", Grade.B),
        },
        Array.Empty<Override>());

    /// <summary>JSON 载入（配置文件形态；结构变更走 change）。</summary>
    public static TrustTable FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var grades = new List<Entry>();
        foreach (var g in root.GetProperty("grades").EnumerateArray())
            grades.Add(new Entry(
                g.GetProperty("producer").GetString()!,
                g.TryGetProperty("category", out var c) ? c.GetString() : null,
                Enum.Parse<Grade>(g.GetProperty("grade").GetString()!, ignoreCase: true)));
        var overrides = new List<Override>();
        if (root.TryGetProperty("overrides", out var os))
            foreach (var o in os.EnumerateArray())
                overrides.Add(new Override(
                    o.GetProperty("package").GetString()!,
                    o.GetProperty("producer").GetString()!,
                    o.GetProperty("category").GetString()!,
                    Enum.Parse<Grade>(o.GetProperty("grade").GetString()!, ignoreCase: true)));
        return new TrustTable(grades, overrides);
    }

    /// <summary>A 级：孤证即可授权常规动作。</summary>
    public static bool CanAuthorizeRoutine(Grade g) => g == Grade.A;

    /// <summary>B 级及以下：不可逆动作前须补佐证（双源或 Tier 1 复查）。</summary>
    public static bool RequiresCorroborationBeforeIrreversible(Grade g) => g != Grade.A;
}
