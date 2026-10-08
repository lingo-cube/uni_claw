namespace UniClaw.Kernel.Capability;

/// <summary>
/// CAP-012 — Language Inspection 能力的协议与身份常量（skill 第 2 步：每能力
/// 一协议）。协议名沿用 Simulation fixture 与协议指南 §8.1 的既有词汇
/// "Language Inspection@1.0"。
/// </summary>
public static class LanguageInspectionProtocol
{
    /// <summary>能力 id（Runtime Integration 域管理面唯一键）。</summary>
    public const string CapabilityId = "runtime.language-inspector";

    /// <summary>协议名：有界文本投影 → 语言格式 Finding 的检查契约。</summary>
    public const string Inspection = "Language Inspection";

    /// <summary>协议版本。</summary>
    public const string Version = "1.0";
}

/// <summary>
/// CAP-012 — Language Inspection 的 L1 协议能力接口：比对观察文本是否符合
/// 期望语言的书写格式（§8.1：read-only 文本投影 → 规则 → Finding，同步确定性）。
/// 输出 Finding 默认非权威；进 Assurance 须另立 Promotion。
/// </summary>
public interface ILanguageInspector : ICapability, ICapabilityProfileReporting
{
    /// <summary>检查一批同 capture 的有界文本项；缺输入/不支持语言诚实 Unknown。</summary>
    Finding Inspect(LanguageInspectorRequest request);
}

/// <summary>
/// CAP-012 — L2 产品实现：确定性 Unicode 脚本规则。expectedLanguage 归一化
/// 前缀（"en-US"→en，"zh-CN"→zh）映射到脚本集（en=Latin，zh=Han）；字母字符
/// 必须属期望脚本，数字/标点/空白/符号中立；任一违例项 → Violation（诊断定位
/// OccurrenceId）。不支持的期望语言、缺输入、无文本可检 → Unknown（诚实不猜）。
/// R7 健康豁免（纯确定性计算）；RL1 常驻无资源，Registered 即稳态。
/// </summary>
public sealed class LanguageFormatInspector : ILanguageInspector
{
    private readonly CapabilityDescription _description;
    private readonly IReadOnlyList<string> _allowlistTerms;

    /// <summary>可选自述声明 + 可选全局术语白名单（D2：词条级，命中即在脚本
    /// 判定前从文本中整词移除；未提供 → 照跑 + 披露影响，D8）。</summary>
    public LanguageFormatInspector(
        CapabilityDescription? description = null,
        IReadOnlyList<string>? allowlistTerms = null)
    {
        _description = description ?? CanonicalDescription;
        var terms = (allowlistTerms ?? Array.Empty<string>())
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Select(term => term.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (terms.Length != (allowlistTerms ?? Array.Empty<string>()).Count(t => t is not null))
            throw new ArgumentException("allowlist terms must be non-empty strings", nameof(allowlistTerms));
        _allowlistTerms = terms;
    }

    /// <summary>ICapability 自述：不可变声明。</summary>
    public CapabilityDescription Description => _description;

    /// <summary>Kernel 拥有的 canonical Definition（Runtime Integration 域）。</summary>
    public static CapabilityDescription CanonicalDescription { get; } = new(
        LanguageInspectionProtocol.CapabilityId, "1.0.0", CapabilityScope.RuntimeIntegration,
        new[] { new CapabilityProtocol(LanguageInspectionProtocol.Inspection, LanguageInspectionProtocol.Version) },
        Array.Empty<CapabilityDependency>(), HealthStatus.Unknown,
        CapabilityCategory.LanguageInspection,
        new[] { new CapabilityRole(CapabilityRoleKind.Realization, "unicode-script-rule") },
        Array.Empty<CapabilityRelationship>());

    /// <summary>同表视图（ModelManagement/UniPerception 先例）：不可变重述。</summary>
    public LanguageFormatInspector WithDescription(CapabilityDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);
        return new(description);
    }

    /// <inheritdoc/>
    public Finding Inspect(LanguageInspectorRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.InputState != InspectorInputState.Accepted || request.Coverage != InspectorCoverage.Full)
            return Unknown(request, "input is incomplete or unavailable");

        var script = ExpectedScript(request.ExpectedLanguage);
        if (script is null)
            return Unknown(request, $"expected language is not supported by this rule: '{request.ExpectedLanguage}'");

        foreach (var item in request.Items)
        {
            var text = item.RenderedText ?? item.DeclaredText ?? string.Empty;
            if (text.Length == 0)
                continue;
            // D1/D2：全局术语白名单（如 WLAN、Wi-Fi）整词移除后再做脚本判定。
            foreach (var term in _allowlistTerms)
                text = RemoveWholeTerm(text, term);
            if (text.Length == 0)
                continue;
            var offending = text.FirstOrDefault(ch => char.IsLetter(ch) && !script(ch));
            if (offending != default)
                return request.CreateFinding(
                    "language-inspection-violation", LanguageInspectionProtocol.CapabilityId,
                    FindingDisposition.Violation, CapabilityStatus.Completed,
                    $"occurrence '{item.OccurrenceId}' contains character 'U+{((int)offending):X4}' outside the expected language script '{request.ExpectedLanguage}'");
        }

        var inspected = request.Items.Count(item =>
            (item.RenderedText ?? item.DeclaredText ?? string.Empty).Length > 0);
        if (inspected == 0)
            return Unknown(request, "no text to inspect");

        return request.CreateFinding(
            "language-inspection-pass", LanguageInspectionProtocol.CapabilityId,
            FindingDisposition.Pass, CapabilityStatus.Completed,
            $"{inspected} text item(s) conform to the expected language format '{request.ExpectedLanguage}'");
    }

    /// <summary>整词移除（大小写不敏感；词边界为非字母字符或串端）。</summary>
    private static string RemoveWholeTerm(string text, string term)
    {
        int index;
        while ((index = text.IndexOf(term, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var before = index == 0 || !char.IsLetter(text[index - 1]);
            var after = index + term.Length >= text.Length || !char.IsLetter(text[index + term.Length]);
            if (!before || !after)
                return text; // 非独立词（如子串嵌在更长词里）：不移除，交脚本判定
            text = text.Remove(index, term.Length);
        }
        return text;
    }

    /// <summary>D7/D8 — 运行剖面：生效配置 + 影响披露 + Limitations。
    /// 未配置白名单 → 照跑 + 披露（跨语言名词会被报违例）。</summary>
    public CapabilityProfileReport DescribeProfile() =>
        new(
            "比对观察文本是否符合期望语言的书写格式（Unicode 脚本级，en=Latin / zh=Han）",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["allowlistTerms"] = _allowlistTerms.Count == 0
                    ? "未加载（0 词条）"
                    : $"{_allowlistTerms.Count} 词条：{string.Join(", ", _allowlistTerms)}",
                ["granularity"] = "文本项级原子判定（菜单级忽略区由宿主任务侧声明，见接线 change）",
            },
            _allowlistTerms.Count == 0
                ? new[] { new CapabilityImpactDisclosure(
                    "未配置全局术语白名单",
                    "跨语言名词（WLAN、Wi-Fi、Bluetooth 等）会被报为违例——检查仍有效，精度受损") }
                : Array.Empty<CapabilityImpactDisclosure>(),
            new[]
            {
                "脚本级判定：字母须属期望脚本，数字/标点/空白中立",
                "不支持的语言（en/zh 之外）诚实 Unknown，不猜",
                "白名单为词条级整词移除，不做模式匹配",
            });

    private static Finding Unknown(LanguageInspectorRequest request, string diagnostic) =>
        request.CreateFinding(
            "language-inspection-unknown", LanguageInspectionProtocol.CapabilityId,
            FindingDisposition.Unknown, CapabilityStatus.Unknown, diagnostic);

    /// <summary>期望语言前缀 → 脚本判定（null = 不支持，诚实 Unknown）。</summary>
    internal static Func<char, bool>? ExpectedScript(string expectedLanguage)
    {
        var prefix = (expectedLanguage ?? string.Empty).Trim().ToLowerInvariant();
        var separator = prefix.IndexOf('-');
        if (separator > 0)
            prefix = prefix[..separator];
        return prefix switch
        {
            "en" => IsLatinLetter,
            "zh" => IsHanCharacter,
            _ => null,
        };
    }

    private static bool IsLatinLetter(char value) =>
        value is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z')
            or (>= '\u00C0' and <= '\u00FF' and not '×' and not '÷');

    private static bool IsHanCharacter(char value) =>
        value is (>= '\u4E00' and <= '\u9FFF') or (>= '\u3400' and <= '\u4DBF') or (>= '\uF900' and <= '\uFAFF');
}
