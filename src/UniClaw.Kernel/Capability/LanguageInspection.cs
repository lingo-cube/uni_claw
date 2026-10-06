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
public interface ILanguageInspector : ICapability
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

    /// <summary>可选自述声明（缺省 canonical；组合根可 WithDescription 附加角色）。</summary>
    public LanguageFormatInspector(CapabilityDescription? description = null)
        => _description = description ?? CanonicalDescription;

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
