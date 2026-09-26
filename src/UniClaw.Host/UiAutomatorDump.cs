using System.Diagnostics;
using System.Xml;
using System.Xml.Linq;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;

namespace UniClaw.Host;

/// <summary>
/// uiautomator XML 树 → Observation claims（PER-009，producer=platform.uiautomator）。
/// 解析为纯函数（fixture 可测，replay 确定）；设备拉取（adb）ENVIRONMENT-gated。
/// 字段角色遵循 changes/PER-009/mechanism.md 冻结表：
///   状态权威 checked(∧checkable=true)/enabled/selected/focused；text=语义文本
///   （像素文字不在树里）；checkable/clickable/scrollable/focusable=能力属性
///   （checkable 是 checked 的 validity guard，非状态）；bounds=空间证据；
///   resource-id/class/package=身份证据。
/// 全部字段以 ui.node.* 私有命名空间落 claim（证据细节层）；共享层
/// （*.state 等）由消费方按 IdentityMatched 解析后映射，解析器不做裁决。
/// </summary>
public static class UiAutomatorDump
{
    public const string Producer = "platform.uiautomator";

    public sealed record NodeInfo(
        string LocalId,
        string? ResourceId,
        string Class,
        string? Package,
        string? Text,
        bool Checkable,
        string Checked, // "false" | "true" | "partial"（三态前向兼容，api35 实发布尔）
        bool Clickable,
        bool Enabled,
        bool Focusable,
        bool Focused,
        bool Scrollable,
        bool Selected,
        string Bounds); // "x1,y1,x2,y2"（px）

    public sealed record DumpResult(
        IReadOnlyList<NodeInfo> Nodes,
        IReadOnlyList<ObservationProposal> Claims);

    /// <summary>
    /// D8 探测状态机（纯逻辑，可测）：服务未启用 → 60s 降级窗口 × 每 Run
    /// ≤3 次探测；超限本 Run 标不可用。瞬时失败不占次数（下周期自然重试）。
    /// </summary>
    public sealed class ProbeStateMachine
    {
        private const int MaxProbesPerRun = 3;
        private readonly TimeSpan _window;
        private DateTimeOffset? _unavailableSince;
        private int _probesThisRun;

        public ProbeStateMachine(TimeSpan? window = null) => _window = window ?? TimeSpan.FromSeconds(60);

        /// <summary>当前是否应尝试 dump（窗口外且未超限）。</summary>
        public bool ShouldProbe(DateTimeOffset now)
        {
            if (_unavailableSince is { } since)
            {
                if (now - since < _window)
                    return false; // 窗口内：不重试
                _unavailableSince = null; // 窗口过期：允许探测
            }
            return _probesThisRun < MaxProbesPerRun;
        }

        /// <summary>服务未启用（结构性失败，区别于瞬时）。</summary>
        public void MarkUnavailable(DateTimeOffset now)
        {
            _unavailableSince = now;
            _probesThisRun++;
        }

        /// <summary>瞬时失败（超时/空输出）：不占探测次数（无状态迁移——
        /// 下周期无条件重试即 D8 语义；保留方法以显式表达协议）。</summary>
        public void MarkTransient() { }

        /// <summary>
        /// C-3（评审修复）：Run 边界显式重置。Host 侧 feed 每 RunOnce 新建
        /// （feed 生命周期 = run 生命周期，重置天然成立）；长生命周期宿主
        /// 复用 feed 时必须在本调用，否则 ≤3 语义退化为永久耗尽。
        /// </summary>
        public void ResetForNewRun()
        {
            _probesThisRun = 0;
            _unavailableSince = null;
        }

        /// <summary>本 Run 内不再尝试（超限后调用方短路）。</summary>
        public bool Exhausted => _probesThisRun >= MaxProbesPerRun;
    }

    /// <summary>
    /// 执行 adb shell uiautomator dump 并返回 XML 字符串。
    /// 失败分类：结构性（服务未启用/权限拒绝）vs 瞬时（超时/空输出）。
    /// 返回 (xml, isStructural)——xml 为 null 时 isStructural 区分 D8 语义。
    /// ENVIRONMENT-gated：需真机/模拟器。
    /// </summary>
    public static (string? Xml, bool IsStructuralFailure) TryDumpToDevice(
        string deviceId, int timeoutMs = 3000)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        // 传输修正（PENDING-ENV 实测）：API 35 上 `dump /dev/tty` 不回显 XML
        // （写文件 + 打印消息）——改为 dump 到定点文件 + cat + rm，单次 shell
        // 原子完成（无二次往返、无残留）。
        const string devicePath = "/data/local/tmp/uniclaw_window_dump.xml";
        var info = new ProcessStartInfo(
            "adb",
            $"-s {deviceId} shell \"uiautomator dump {devicePath} >/dev/null 2>&1 && cat {devicePath} && rm -f {devicePath}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        // S-1（评审修复）：先异步起读再限时等待——ReadToEnd 阻塞在
        // WaitForExit 之前会让超时永远约束不了读取；fail-closed 的
        // InvalidOperationException 不吞（只把可分类的执行层异常归瞬时）。
        try
        {
            using var process = Process.Start(info)
                ?? throw new InvalidOperationException("adb 启动失败（fail-closed）");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch (System.ComponentModel.Win32Exception) { }
                _ = process.WaitForExit(1000);
                return (null, IsStructural: false); // 超时：瞬时（不占探测次数）
            }
            var stdout = stdoutTask.GetAwaiter().GetResult();
            if (process.ExitCode != 0)
                return (null, IsStructural: false); // 瞬时（shell 层失败）
            var xmlStart = stdout.IndexOf("<?xml", StringComparison.Ordinal);
            if (xmlStart < 0)
                return (null, IsStructural: true); // dump 输出无 XML = 服务未启用
            return (stdout[xmlStart..], IsStructural: false);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            return (null, IsStructural: false); // 可分类执行层异常：瞬时；fail-closed 异常上抛
        }
    }

    /// <summary>
    /// 解析 dump XML（纯函数）。空树 = OK_EMPTY：零 claims、非失败
    /// （A8：缺检测不产观察）。非法 XML → XmlException 显式抛出
    /// （fail-closed；transport/执行层应先分类超时与失败）。
    /// </summary>
    public static DumpResult Parse(string xml, DateTimeOffset captureTime, ObservationContext context)
    {
        ArgumentNullException.ThrowIfNull(xml);
        var root = XDocument.Parse(xml).Root ?? throw new InvalidOperationException("dump XML 无根节点（SchemaFailure，fail-closed）");
        var nodes = new List<NodeInfo>();
        var claims = new List<ObservationProposal>();
        var index = 0;
        foreach (var element in root.Descendants("node"))
        {
            var node = ToNodeInfo(element, index++);
            nodes.Add(node);
            AddNodeClaims(claims, node, captureTime, context);
        }
        return new DumpResult(nodes, claims);
    }

    /// <summary>
    /// IdentityMatched 辅助（D3）：按净化 resource-id 唯一解析节点。
    /// 零命中或多命中 → null（= 剥夺 XML 权威，非错误）。
    /// </summary>
    public static NodeInfo? ResolveUniqueByResourceId(DumpResult dump, string localId)
    {
        ArgumentNullException.ThrowIfNull(dump);
        NodeInfo? hit = null;
        var count = 0;
        foreach (var n in dump.Nodes)
        {
            if (n.ResourceId is null || Sanitize(n.ResourceId) != localId)
                continue;
            hit = n;
            count++;
            if (count > 1)
                return null;
        }
        return count == 1 ? hit : null;
    }

    /// <summary>
    /// P-3（评审修复）：跨源碰头——目标 bounds 与 XML 节点空间映射。
    /// 唯一最佳重叠（IoU ≥ 0.5 且领先次名 ≥ 0.25 余量）且 checkable=true
    /// （D12 guard）时，产 {role}.state 共享 claim（producer=XML），值域
    /// {on,off,partial}；lineage 携带 C-1 Kernel 侧裁决所需快照
    /// （xml-map:{localId} / xml-checkable / xml-unique）。非唯一/不重叠/
    /// 非 checkable → null（缺席即数据，不映射不裁决）。
    /// </summary>
    public static ObservationProposal? MapTargetStateClaim(
        DumpResult dump,
        string role,
        (double X1, double Y1, double X2, double Y2) targetNormalizedBounds,
        int viewportWidth,
        int viewportHeight,
        DateTimeOffset captureTime,
        ObservationContext context)
    {
        ArgumentNullException.ThrowIfNull(dump);

        NodeInfo? best = null;
        var bestOverlap = 0.0;
        var secondBest = 0.0;
        foreach (var node in dump.Nodes)
        {
            var overlap = NormalizedOverlap(node, targetNormalizedBounds, viewportWidth, viewportHeight);
            if (overlap > bestOverlap)
            {
                secondBest = bestOverlap;
                bestOverlap = overlap;
                best = node;
            }
            else if (overlap > secondBest)
            {
                secondBest = overlap;
            }
        }

        if (best is null || bestOverlap < 0.5 || bestOverlap - secondBest < 0.25)
            return null; // 非唯一/不重叠：不映射
        if (!best.Checkable)
            return null; // D12 guard：checkable=false 时 checked 不具权威

        var value = best.Checked switch { "true" => "on", "false" => "off", var v => v };
        var subject = $"{role}.state";
        return new ObservationProposal(
            new ObservationClaim(subject, value),
            IngressKind.Observation,
            context,
            new Provenance(Producer, captureTime, $"scope:{subject}",
                new[] { $"xml-map:{best.LocalId}", $"xml-checkable:{best.Checkable}", "xml-unique:true" }));
    }

    /// <summary>节点 bounds（px）归一化后与目标 bounds 的 IoU；解析失败 = 0。</summary>
    private static double NormalizedOverlap(
        NodeInfo node,
        (double X1, double Y1, double X2, double Y2) target,
        int viewportWidth,
        int viewportHeight)
    {
        var parts = node.Bounds.Split(',');
        if (parts.Length != 4
            || !double.TryParse(parts[0], out var x1) || !double.TryParse(parts[1], out var y1)
            || !double.TryParse(parts[2], out var x2) || !double.TryParse(parts[3], out var y2))
            return 0;
        if (viewportWidth <= 0 || viewportHeight <= 0 || x2 <= x1 || y2 <= y1)
            return 0;

        var nx1 = x1 / viewportWidth;
        var ny1 = y1 / viewportHeight;
        var nx2 = x2 / viewportWidth;
        var ny2 = y2 / viewportHeight;

        var ix1 = Math.Max(nx1, target.X1);
        var iy1 = Math.Max(ny1, target.Y1);
        var ix2 = Math.Min(nx2, target.X2);
        var iy2 = Math.Min(ny2, target.Y2);
        if (ix2 <= ix1 || iy2 <= iy1)
            return 0;
        var intersection = (ix2 - ix1) * (iy2 - iy1);
        var union = (nx2 - nx1) * (ny2 - ny1)
                    + (target.X2 - target.X1) * (target.Y2 - target.Y1)
                    - intersection;
        return union <= 0 ? 0 : intersection / union;
    }

    internal static string Sanitize(string resourceId)
    {
        var tail = resourceId;
        var lastColon = tail.LastIndexOf(':');
        if (lastColon >= 0)
            tail = tail[(lastColon + 1)..];
        var lastSlash = tail.LastIndexOf('/');
        if (lastSlash >= 0)
            tail = tail[(lastSlash + 1)..];
        return tail;
    }

    private static NodeInfo ToNodeInfo(XElement e, int index)
    {
        var resourceId = (string?)e.Attribute("resource-id");
        var localId = string.IsNullOrEmpty(resourceId) ? $"node{index}" : Sanitize(resourceId);
        return new NodeInfo(
            LocalId: localId,
            ResourceId: resourceId,
            Class: (string?)e.Attribute("class") ?? string.Empty,
            Package: (string?)e.Attribute("package"),
            Text: (string?)e.Attribute("text"),
            Checkable: Bool(e, "checkable"),
            Checked: CheckedValue(e),
            Clickable: Bool(e, "clickable"),
            Enabled: Bool(e, "enabled"),
            Focusable: Bool(e, "focusable"),
            Focused: Bool(e, "focused"),
            Scrollable: Bool(e, "scrollable"),
            Selected: Bool(e, "selected"),
            Bounds: ParseBounds((string?)e.Attribute("bounds")));
    }

    private static string CheckedValue(XElement e)
    {
        var raw = (string?)e.Attribute("checked");
        return raw switch
        {
            "true" => "true",
            "partial" => "partial", // API 36+ 前向兼容通道（api35 实发布尔）
            _ => "false",
        };
    }

    private static bool Bool(XElement e, string name) =>
        (string?)e.Attribute(name) == "true";

    private static string ParseBounds(string? raw)
    {
        // uiautomator 格式："[x1,y1][x2,y2]" → "x1,y1,x2,y2"
        if (raw is null)
            return "0,0,0,0";
        var parts = raw.Replace("][", ",").Replace("[", "").Replace("]", "").Split(',');
        return parts.Length == 4 ? string.Join(",", parts) : "0,0,0,0";
    }

    private static void AddNodeClaims(
        List<ObservationProposal> claims, NodeInfo node, DateTimeOffset captureTime, ObservationContext context)
    {
        void Add(string field, string value)
        {
            var subject = $"ui.node.{node.LocalId}.{field}";
            claims.Add(new ObservationProposal(
                new ObservationClaim(subject, value),
                IngressKind.Observation,
                context,
                new Provenance(Producer, captureTime, $"scope:{subject}", new[] { "uiautomator:dump" })));
        }

        Add("class", node.Class);
        if (node.ResourceId is { Length: > 0 })
            Add("resource_id", node.ResourceId);
        if (node.Package is { Length: > 0 })
            Add("package", node.Package);
        if (node.Text is { Length: > 0 })
            Add("text", node.Text);
        // 状态权威字段（checked 的有效性 guard=checkable 由消费方判定——解析层全量落证）
        Add("checked", node.Checked);
        Add("checkable", node.Checkable ? "true" : "false");
        Add("clickable", node.Clickable ? "true" : "false");
        Add("enabled", node.Enabled ? "true" : "false");
        Add("focusable", node.Focusable ? "true" : "false");
        Add("focused", node.Focused ? "true" : "false");
        Add("scrollable", node.Scrollable ? "true" : "false");
        Add("selected", node.Selected ? "true" : "false");
        Add("bounds", node.Bounds);
    }

    // ====================================================================
    // PER-013 Slice B：typed 解析（legacy XML → UiHierarchyObservation v1）
    // ====================================================================

    /// <summary>
    /// typed 解析的调用方输入（acquisition 侧持有设备/会话事实；API level 由
    /// 调用方经 adb 查询提供——PER-010 metadata 必填，不做默认猜测）。
    /// </summary>
    public sealed record UiHierarchyParseContext(
        string CaptureId,
        DateTimeOffset CaptureTimestamp,
        string DeviceId,
        string SessionCorrelation,
        int AndroidApiLevel,
        string? ObservationCycleId = null,
        TimeSpan? CaptureDuration = null,
        string AcquirerVersion = "adb-uiautomator/legacy",
        HierarchyCapabilities? Capabilities = null,
        CheckedExactProof? ExactProof = null,
        CoordinateSpace? Space = null);

    /// <summary>
    /// legacy XML 的默认能力声明（PER-013 Slice B）：semantic text /
    /// content-description / collapsed checked。visible-to-user、hint、
    /// drawing-order、window、Compose、WebView 未声明——对应字段一律
    /// <c>Unsupported</c>（capability &gt; acquirer &gt; API level）。
    /// </summary>
    public static HierarchyCapabilities LegacyXmlDefaultCapabilities { get; } =
        new(HierarchyCapability.SemanticText
            | HierarchyCapability.ContentDescription
            | HierarchyCapability.CheckedBooleanCollapsed);

    /// <summary>
    /// PER-013 Slice B：legacy uiautomator XML → <see cref="UiHierarchyCaptureResult"/>
    /// （typed、parse 层不再有 missing→false / bounds 默认值折叠）。
    /// adapter field policy（显式声明，F-E4）：
    /// - 结构非法（XmlException / 无根）→ outcome=Malformed，无部分节点；
    /// - 字段级无法解析（布尔/checked/bounds 非法值、倒置 bounds）→ 该字段
    ///   Unknown(reason)，不使整 capture Malformed、不猜默认值；
    /// - 属性缺席 → Unknown(attribute-missing)；空串文本 → Observed("")（F-B1）；
    /// - 未知属性名 → 忽略（不猜值、不报错、不进 ObservedValue；F-E1）；
    /// - checked=false 按能力映射：默认 collapsed → Unknown(partial-unrepresentable)
    ///   （PER-012 M-02）；仅 ExactProof 三项完整才 Unchecked（M-01）；
    /// - checked="partial"：capability 声明 triState 才无损映射，否则
    ///   Unknown(partial-unrepresentable)；
    /// - password=true 节点的 Text/ContentDescription/Hint 脱敏为
    ///   "[redacted:password]"，provenance.Normalization 记 redact:password（F-E2）。
    /// 零节点 → Empty（≠ 世界 absence）；否则 Complete。
    /// </summary>
    public static UiHierarchyCaptureResult ParseHierarchyObservation(
        string xml, UiHierarchyParseContext parseContext)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(parseContext);

        var capabilities = parseContext.Capabilities ?? LegacyXmlDefaultCapabilities;
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (XmlException ex)
        {
            return new UiHierarchyCaptureResult(
                UiHierarchyCaptureOutcome.Malformed, null, null,
                Diagnostic: $"xml-structure-unparseable:{ex.GetType().Name}");
        }

        var root = document.Root;
        if (root is null || root.Name != "hierarchy")
        {
            return new UiHierarchyCaptureResult(
                UiHierarchyCaptureOutcome.Malformed, null, null,
                Diagnostic: "xml-structure-unparseable:root-not-hierarchy");
        }

        var metadata = new CaptureMetadata(
            CaptureId: parseContext.CaptureId,
            AndroidApiLevel: parseContext.AndroidApiLevel,
            AcquirerKind: UiHierarchyAcquirerKind.LegacyUiAutomatorXml,
            AcquirerVersion: parseContext.AcquirerVersion,
            HierarchyFormat: UiHierarchyFormat.UiAutomatorXml,
            CaptureTimestamp: parseContext.CaptureTimestamp,
            CaptureDuration: parseContext.CaptureDuration,
            DeviceId: parseContext.DeviceId,
            SessionCorrelation: parseContext.SessionCorrelation,
            ObservationCycleId: parseContext.ObservationCycleId,
            Capabilities: capabilities,
            Coverage: new HierarchyCoverage(CoverageCompleteness.CompleteWithinDeclaredSurface));

        var nodes = new List<UiNodeObservation>();
        var localIndex = 0;
        foreach (var element in root.Elements("node"))
            AppendTypedNode(element, parseContext.CaptureId, capabilities, parseContext.ExactProof, nodes, ref localIndex, parent: null);

        var observation = new UiHierarchyObservation(metadata, Array.Empty<UiWindowOccurrence>(), nodes);
        var outcome = nodes.Count == 0 ? UiHierarchyCaptureOutcome.Empty : UiHierarchyCaptureOutcome.Complete;
        var result = new UiHierarchyCaptureResult(outcome, metadata, observation, Diagnostic: null);
        result.Validate();
        return result;
    }

    private static void AppendTypedNode(
        XElement element,
        string captureId,
        HierarchyCapabilities capabilities,
        CheckedExactProof? exactProof,
        List<UiNodeObservation> nodes,
        ref int localIndex,
        OccurrenceRef? parent)
    {
        var occurrence = new OccurrenceRef(captureId, localIndex++);
        var provenance = new FieldProvenance(captureId);

        var password = BoolField(element, "password", provenance);
        var redact = password.TryGetObserved(out var passwordValue) && passwordValue;
        var text = redact
            ? RedactedStringField(provenance)
            : StringField(element, "text", provenance);
        var contentDescription = redact
            ? RedactedStringField(provenance)
            : StringField(element, "content-desc", provenance);

        ObservedValue<string> hint = capabilities.Has(HierarchyCapability.Hint)
            ? StringField(element, "hint", provenance)
            : ObservedValue<string>.Unsupported("capability:hint-absent", provenance);
        var visibleToUser = capabilities.Has(HierarchyCapability.Visibility)
            ? BoolField(element, "visible-to-user", provenance)
            : ObservedValue<bool>.Unsupported("capability:visibility-absent", provenance);

        var node = new UiNodeObservation(
            OccurrenceRef: occurrence,
            ParentOccurrenceRef: parent,
            WindowOccurrenceRef: null,
            SiblingOrder: IntField(element, "index"),
            DrawingOrder: null,
            Class: StringField(element, "class", provenance),
            ResourceId: StringField(element, "resource-id", provenance),
            Package: StringField(element, "package", provenance),
            Text: text,
            ContentDescription: contentDescription,
            Hint: hint,
            Checkable: BoolField(element, "checkable", provenance),
            Checked: CheckedField(element, capabilities, exactProof, provenance),
            Enabled: BoolField(element, "enabled", provenance),
            Selected: BoolField(element, "selected", provenance),
            Focused: BoolField(element, "focused", provenance),
            Scrollable: BoolField(element, "scrollable", provenance),
            Clickable: BoolField(element, "clickable", provenance),
            Focusable: BoolField(element, "focusable", provenance),
            VisibleToUser: visibleToUser,
            Password: password,
            Bounds: BoundsField(element, provenance));
        nodes.Add(node);

        foreach (var child in element.Elements("node"))
            AppendTypedNode(child, captureId, capabilities, exactProof, nodes, ref localIndex, parent: occurrence);
    }

    private static ObservedValue<string> StringField(
        XElement element, string name, FieldProvenance provenance)
    {
        var attribute = element.Attribute(name);
        return attribute is null
            ? ObservedValue<string>.Unknown("attribute-missing", provenance)
            : ObservedValue<string>.Observed(attribute.Value, provenance with { SourceField = name });
    }

    private static ObservedValue<string> RedactedStringField(FieldProvenance provenance) =>
        ObservedValue<string>.Observed(
            "[redacted:password]",
            provenance with { Normalization = "redact:password" });

    private static ObservedValue<bool> BoolField(
        XElement element, string name, FieldProvenance provenance) =>
        element.Attribute(name)?.Value switch
        {
            null => ObservedValue<bool>.Unknown("attribute-missing", provenance),
            "true" => ObservedValue<bool>.Observed(true, provenance with { SourceField = name }),
            "false" => ObservedValue<bool>.Observed(false, provenance with { SourceField = name }),
            _ => ObservedValue<bool>.Unknown("malformed-field", provenance with { SourceField = name }),
        };

    private static int? IntField(XElement element, string name) =>
        int.TryParse(element.Attribute(name)?.Value, out var value) ? value : null;

    private static ObservedValue<CheckedState> CheckedField(
        XElement element, HierarchyCapabilities capabilities, CheckedExactProof? exactProof, FieldProvenance provenance)
    {
        var raw = element.Attribute("checked")?.Value;
        var declared = capabilities.ResolveCheckedCapability();
        if (declared is null)
        {
            return ObservedValue<CheckedState>.Unsupported("capability:checked-absent", provenance);
        }

        switch (raw)
        {
            case null:
                return ObservedValue<CheckedState>.Unknown("attribute-missing", provenance);
            case "true":
            case "false":
                return CheckedSemantics.MapBoolean(raw == "true", declared.Value, exactProof, provenance);
            case "partial":
                return declared == CheckedCapability.CheckedTriState
                    ? CheckedSemantics.MapTriState(CheckedState.Partial, provenance)
                    : ObservedValue<CheckedState>.Unknown(CheckedSemantics.PartialUnrepresentableReason, provenance);
            default:
                return ObservedValue<CheckedState>.Unknown("malformed-field", provenance with { SourceField = "checked" });
        }
    }

    private static ObservedValue<UiBounds> BoundsField(XElement element, FieldProvenance provenance)
    {
        var raw = element.Attribute("bounds")?.Value;
        if (raw is null)
        {
            return ObservedValue<UiBounds>.Unknown("attribute-missing", provenance);
        }

        // uiautomator 格式 "[x1,y1][x2,y2]"
        var parts = raw.Replace("][", ",").Replace("[", "").Replace("]", "").Split(',');
        if (parts.Length != 4
            || !int.TryParse(parts[0], out var x1) || !int.TryParse(parts[1], out var y1)
            || !int.TryParse(parts[2], out var x2) || !int.TryParse(parts[3], out var y2))
        {
            return ObservedValue<UiBounds>.Unknown("malformed-bounds", provenance with { SourceField = "bounds" });
        }

        var bounds = new UiBounds(x1, y1, x2, y2);
        return bounds.IsValid
            ? ObservedValue<UiBounds>.Observed(bounds, provenance with { SourceField = "bounds" })
            : ObservedValue<UiBounds>.Unknown("malformed-bounds", provenance with { SourceField = "bounds" });
    }

    // ====================================================================
    // PER-013 Slice B：A-4 测试债的可测缝（PER-009 D8 语义，行为不变）
    // ====================================================================

    /// <summary>
    /// D8 XML 并行观察的决策核心（internal test seam，PER-009 A-4 测试债）：
    /// 探测门（60s×≤3）→ 传输 → 结构性/瞬时分类 → Parse。行为与
    /// LivePerception.TryCoObserveXml 原实现一致；live 侧委托本核心。
    /// PER-013 Slice E：同时返回原始 <paramref name="Xml"/>（typed 路由需要；
    /// Dump 失败/非法时为 null）。
    /// </summary>
    /// <param name="probe">D8 探测状态机。</param>
    /// <param name="now">当前时刻。</param>
    /// <param name="captureTime">成功 dump 的 capture 时刻。</param>
    /// <param name="context">观察上下文。</param>
    /// <param name="transport">设备传输（可注入；live = TryDumpToDevice）。</param>
    internal static (UiAutomatorDump.DumpResult? Dump, string? Xml, bool Degraded) CoObserveXml(
        ProbeStateMachine probe,
        DateTimeOffset now,
        DateTimeOffset captureTime,
        ObservationContext context,
        Func<(string? Xml, bool IsStructural)> transport)
    {
        if (!probe.ShouldProbe(now))
        {
            return (null, null, probe.Exhausted); // 超限后持续标记 degraded
        }

        var (xml, isStructural) = transport();
        if (xml is null)
        {
            if (isStructural)
                probe.MarkUnavailable(now); // D8 结构性：占探测次数
            else
                probe.MarkTransient();      // D8 瞬时：不占，下周期重试
            return (null, null, true);
        }

        try
        {
            return (Parse(xml, captureTime, context), xml, false);
        }
        catch (XmlException)
        {
            probe.MarkTransient(); // 非法 XML：瞬时（fail-closed，不产观察）
            return (null, null, true);
        }
    }

    /// <summary>
    /// PER-013 Slice E（M-06）：live feed XML 证据的组合面——typed 路由
    /// （per-node occurrence-qualified claims）完全替代 legacy `dump.Claims`
    /// per-node 通道（observer 级 cutover；`ui.node.*` 零决策消费方，reader
    /// inventory 见 changes/PER-013）。legacy `*.state` 映射 claim
    /// （effect-critical，PER-012 egress surface）按原相位规则保留——两类
    /// 路由各只产各的 subject，无 dual-read。
    /// typed 路由不可用（xml 缺席 / typedContext null，如 API level 未知）→
    /// per-node 证据诚实缺席，不回退 legacy 通道（rollback = routing-only，
    /// 须显式并标 legacy/degraded）。
    /// </summary>
    /// <param name="xml">原始 dump XML（null = 本次无 XML）。</param>
    /// <param name="mappedLegacyStateClaim">MapTargetStateClaim 输出（legacy 路由）。</param>
    /// <param name="isPostPhase">post-action 相位（initial 相位才并置映射 claim）。</param>
    /// <param name="typedContext">typed 解析上下文（null = typed 路由不可用）。</param>
    /// <param name="context">观察上下文。</param>
    internal static ObservationProposal[] ComposeXmlEvidence(
        string? xml,
        ObservationProposal? mappedLegacyStateClaim,
        bool isPostPhase,
        UiHierarchyParseContext? typedContext,
        ObservationContext context)
    {
        var extras = new List<ObservationProposal>();
        if (xml is not null && typedContext is not null
            && ParseHierarchyObservation(xml, typedContext).Observation is { } observation)
        {
            extras.AddRange(TypedHierarchyProposalProjector.Project(observation, context));
        }

        if (!isPostPhase && mappedLegacyStateClaim is { } mapped)
        {
            extras.Add(mapped);
        }

        return extras.ToArray();
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _apiLevelCache = new();

    /// <summary>
    /// 设备 API level（typed CaptureMetadata 必填事实；PER-010 不做默认猜测）。
    /// adb getprop 单次查询 + 进程内缓存；失败 → null（typed 路由诚实缺席）。
    /// </summary>
    internal static int? TryGetApiLevel(string deviceId)
    {
        if (_apiLevelCache.TryGetValue(deviceId, out var cached))
        {
            return cached;
        }

        try
        {
            var info = new ProcessStartInfo("adb", $"-s {deviceId} shell getprop ro.build.version.sdk")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start(info);
            if (process is null)
            {
                return null;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);
            return int.TryParse(stdout.Trim(), out var level) && level > 0
                ? _apiLevelCache.GetOrAdd(deviceId, level)
                : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null; // 传输层失败：typed metadata 不完整 → 不产 typed 证据
        }
    }

    /// <summary>
    /// 缺席标记（D1：缺席即数据）：proposals 的 provenance lineage 追加
    /// "degraded:no-xml"；provenance 为 null 的保持 null（不造 provenance）。
    /// internal test seam（A-4）。
    /// </summary>
    internal static ObservationProposal[] TagDegradedNoXml(IReadOnlyList<ObservationProposal> proposals) =>
        proposals.Select(p => p with
        {
            Provenance = p.Provenance is null
                ? null
                : p.Provenance with
                {
                    TransformationLineage = p.Provenance.TransformationLineage
                        .Append("degraded:no-xml").ToArray(),
                },
        }).ToArray();
}
