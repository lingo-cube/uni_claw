using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Host;

/// <summary>
/// Real Android observation feed for the Settings traversal buyer. Each call
/// captures a fresh screenshot and hierarchy, keeps their independent ids, and
/// emits only Product observation proposals. The fast vision result is recorded
/// as evidence telemetry when available; it never becomes an action target.
/// </summary>
public sealed class SettingsTraversalLiveFeed : IDisposable
{
    // The Android Settings Wi-Fi preference is a declared two-state test
    // target. This is an explicit fixture-backed contract, not an inference
    // from API level, class name, or a sample capture.
    private static readonly HierarchyCapabilities WifiToggleCapabilities =
        new(HierarchyCapability.SemanticText
            | HierarchyCapability.ContentDescription
            | HierarchyCapability.CheckedBooleanExact);
    private static readonly CheckedExactProof WifiToggleExactProof = new(
        "contract:settings.wifi-toggle/two-state",
        "capability:android-settings-profile/wifi-switch/exact",
        "testsets/android-settings/wifi-toggle-contract.json");

    public sealed record TraceEntry(
        int Cycle,
        string Context,
        string CaptureId,
        string ObservationCycleId,
        bool FastAvailable,
        bool HierarchyAvailable,
        int ProposalCount,
        TimeSpan FastLatency,
        TimeSpan HierarchyLatency,
        string ScreenIdentity,
        string? Popup = null,
        string? Slow = null,
        // AGT-010 §2：视口摘要（与 RouteKey 独立；null = 无 XML 无声明）
        string? ViewportDigest = null,
        string? Grounding = null,
        string? Assurance = null,
        string? Effect = null,
        string? Verification = null,
        // 快路径总耗时包含首次视觉服务冷启动；分段字段用于定位真实瓶颈。
        TimeSpan? FastServiceStartupLatency = null,
        TimeSpan? FastDecodeLatency = null,
        TimeSpan? FastInferenceLatency = null,
        bool? FastInferenceSucceeded = null,
        TimeSpan? FastServiceWarmupLatency = null,
        bool? FastServiceWarmupSucceeded = null,
        string? FastServiceWarmupError = null);

    private readonly HostUtilities.VirtualClock _clock;
    private readonly LivePerception.LiveAssets _assets;
    private readonly AdbScreenshotAcquisition _acquisition;
    private readonly VisionServiceSession _vision;
    private readonly UiAutomatorDump.ProbeStateMachine _probe = new();
    private readonly List<TraceEntry> _trace = new();
    private readonly string? _evidenceDir;
    private readonly bool _persistScreenshots;
    private readonly bool _persistHierarchies;
    private readonly SettingsCoverageConfig? _coverageConfig;
    private readonly VisionServiceSession.WarmupTiming _warmup;
    private Func<SlowConsultationRequest, UniKernel, bool, TimeSpan?, SlowConsultationOutcome> _slowConsult;
    private int _popupPresentStreak;
    private int _slowRequests;
    private int _cycle;

    public SettingsTraversalLiveFeed(HostUtilities.VirtualClock clock, LivePerception.LiveAssets assets)
        : this(clock, assets, null)
    {
    }

    /// <summary>AGT-008：evidenceDir 非 null 时每周期落盘 {captureId}.png/.xml
    /// （文件名与步骤记录 postCaptureId 直接关联；写失败 fail closed）。
    /// AGT-017：slowConsult 非 null 时替代默认回放桩（live 模型桥注入缝）。</summary>
    public SettingsTraversalLiveFeed(
        HostUtilities.VirtualClock clock,
        LivePerception.LiveAssets assets,
        string? evidenceDir,
        bool persistScreenshots = true,
        bool persistHierarchies = true,
        SettingsCoverageConfig? coverageConfig = null,
        Func<SlowConsultationRequest, UniKernel, bool, TimeSpan?, SlowConsultationOutcome>? slowConsult = null)
    {
        _clock = clock;
        _assets = assets;
        _acquisition = new AdbScreenshotAcquisition(assets.DeviceId, "adb", () => clock.Now);
        _vision = new VisionServiceSession(assets.ProviderRoot, assets.PythonExecutable, assets.CacheRoot);
        _evidenceDir = evidenceDir;
        _persistScreenshots = persistScreenshots;
        _persistHierarchies = persistHierarchies;
        _coverageConfig = coverageConfig;
        // Keep service cold-start outside the first Agent observation window.
        // This is a Host lifecycle optimization; the model, protocol and
        // observation payload remain unchanged.
        _warmup = _vision.Warmup();
        _slowConsult = slowConsult ?? ((request, kernel, effectCritical, boundedWait) =>
            new SlowConsultation().Consult(request, kernel, effectCritical, boundedWait));
    }

    public IReadOnlyList<TraceEntry> Trace => _trace;

    public VisionServiceSession.WarmupTiming EnvironmentPreflight => _warmup;

    /// <summary>AGT-009：kernel 访问缝（runner 注入）——Slow 结果投影目标；
    /// 未注入或 Slow 关闭时不发起咨询。</summary>
    internal Func<UniKernel?>? KernelProvider { get; set; }

    /// <summary>AGT-009：测试缝——替换默认 SlowConsultation 组合（确定性桩）。</summary>
    internal Func<SlowConsultationRequest, UniKernel, bool, TimeSpan?, SlowConsultationOutcome> SlowConsultOverride
    {
        set => _slowConsult = value;
    }

    public RunDriverInput? Next(ObservationDirective directive)
    {
        if (directive.Context == ObservationContext.PostActionEffectFlow)
            Thread.Sleep(1200);

        _clock.Tick();
        var cycle = ++_cycle;
        var observationCycleId = $"settings-cycle-{cycle:D3}";
        var capture = _acquisition.CaptureAsync(CancellationToken.None).GetAwaiter().GetResult();
        var captureId = $"capture-{Guid.NewGuid():N}";
        if (_evidenceDir is not null && _persistScreenshots)
            WriteEvidenceFile(_evidenceDir, captureId + ".png", capture.Artifact.Payload);

        var fastStart = Stopwatch.GetTimestamp();
        var fastBasis = TryFast(capture.Artifact.Payload, captureId, observationCycleId,
            capture.Artifact.Metadata.CaptureTime);
        var fastAvailable = fastBasis?.HasDetection == true;
        var fastLatency = Stopwatch.GetElapsedTime(fastStart);

        var hierarchyStart = Stopwatch.GetTimestamp();
        var xmlResult = TryDump(directive.Context, observationCycleId, captureId,
            capture.Width, capture.Height);
        var hierarchyLatency = Stopwatch.GetElapsedTime(hierarchyStart);
        // AGT-010 §2：多信号 RouteKey（标题×来源×up×滚动容器）；无 XML →
        // 回退身份（AGT-006 语义）。视口摘要独立计算（与 key 字段集不相交）。
        var routeKey = xmlResult.Xml is null
            ? "android.settings"
            : DeriveRouteKey(xmlResult.Xml);
        var viewportDigest = DeriveViewportDigest(xmlResult.Xml);
        // AGT-009：弹窗结构分类——XML 可解析才有声明；与同一周期的其他 proposal
        // 同批进入证据流。
        var popup = _coverageConfig?.PopupSettings;
        var popupState = DerivePopupState(xmlResult.Xml,
            popup?.HostPackage ?? "com.android.settings",
            popup?.ResourceIds ?? SettingsCoverage.PopupClearanceConfig.DefaultPopupResourceIds);

        if (_evidenceDir is not null && _persistHierarchies && xmlResult.Xml is not null)
            WriteEvidenceFile(_evidenceDir, captureId + ".xml",
                System.Text.Encoding.UTF8.GetBytes(xmlResult.Xml));

        var proposals = new List<ObservationProposal>
        {
            new(
                new ObservationClaim(ProductAssociationStrategy.ScreenIdentitySubject, "android.settings"),
                IngressKind.Observation,
                directive.Context,
                new Provenance("host.live.settings", _clock.Now, "scope:ui.screen",
                    new[] { "real-device", "capture:" + captureId }))
        };
        proposals.Add(new ObservationProposal(
            new ObservationClaim(ProductAssociationStrategy.ScreenRouteSubject, routeKey),
            IngressKind.Observation,
            directive.Context,
            new Provenance("host.live.settings", _clock.Now,
                $"scope:ui.screen.route:{observationCycleId}",
                new[] { "real-device", "capture:" + captureId, "route-fingerprint", "route-key:rk1" })));
        var popupProposal = PopupProposal(popupState, captureId, observationCycleId, _clock.Now, directive.Context);
        if (popupProposal is not null)
            proposals.Add(popupProposal);

        // AGT-009 §8：有界 Slow 触发（默认关闭；预算/超时/config 三重收口）。
        var slowTrace = ConsultSlowIfTriggered(
            trigger: DeriveSlowTrigger(
                xmlResult.Xml is not null, fastAvailable, CountClickableNodes(xmlResult.Xml), routeKey,
                UpdatePopupStreak(popupState), _coverageConfig?.SlowSettings),
            captureId, observationCycleId, capture.Artifact.Payload, fastAvailable, fastBasis);
        if (xmlResult.Xml is not null)
        {
            var api = UiAutomatorDump.TryGetApiLevel(_assets.DeviceId);
            if (api is not null)
            {
                var typed = UiAutomatorDump.ParseHierarchyObservation(xmlResult.Xml,
                    new UiAutomatorDump.UiHierarchyParseContext(
                        captureId, _clock.Now, _assets.DeviceId,
                        $"settings:{_assets.DeviceId}", api.Value,
                        ObservationCycleId: observationCycleId,
                        Capabilities: WifiToggleCapabilities,
                        ExactProof: WifiToggleExactProof,
                        Space: CoordinateSpace.DeviceViewport(capture.Width, capture.Height)));
                if (typed.Observation is { } observation)
                    proposals.AddRange(TypedHierarchyProposalProjector.Project(observation, directive.Context));
            }
        }

        _trace.Add(new TraceEntry(cycle, directive.Context.ToString(), captureId,
            observationCycleId, fastAvailable, xmlResult.Xml is not null,
            proposals.Count, fastLatency, hierarchyLatency, routeKey,
            popupState, slowTrace, viewportDigest,
            FastServiceStartupLatency: _vision.LastAnalyzeTiming?.ServiceStartup,
            FastDecodeLatency: _vision.LastAnalyzeTiming?.PngDecode,
            FastInferenceLatency: _vision.LastAnalyzeTiming?.Inference,
            FastInferenceSucceeded: _vision.LastAnalyzeTiming?.Succeeded,
            FastServiceWarmupLatency: cycle == 1 ? _warmup.Duration : null,
            FastServiceWarmupSucceeded: cycle == 1 ? _warmup.Succeeded : null,
            FastServiceWarmupError: cycle == 1 ? _warmup.Error : null));
        return new RunDriverInput.Observation(proposals);
    }

    /// <summary>
    /// AGT-010 §2 — 多信号 RouteKey（页面身份）：标题 × 标题来源（homepage_title
    /// = 根页 / title = 二级页）× up 按键在场。字段以 e1 实录语料（23 份 XML）
    /// 离线区分度分析冻结（changes/AGT-010 Decisions 1）：撞名页（Security &amp;
    /// privacy，标题 "Settings"）与根页在 src/up 两信号全不同；根页 12 份采样
    /// （含滚动前后三种视口态）key 全稳定。滚动容器信号经语料证伪被剔除：
    /// 首页建议条容器随视口出现/消失（初始视口 2 容器、滚动后 1 容器），
    /// first/last 选取亦不稳定——页面身份必须滚动不变。条目集同理不进 key。
    /// 无标题或不可解析 → 回退身份 android.settings（AGT-006 未知页语义不变）。
    /// 只用于 WorldModel container association / 路由指纹；不参与 target
    /// grounding，也不制造坐标或 resource-id。
    /// </summary>
    internal static string DeriveRouteKey(string xml)
    {
        try
        {
            var root = XDocument.Parse(xml).Root;
            if (root is null)
                return "android.settings";
            var nodes = root.Descendants("node").ToList();
            string? title = null;
            var source = "none";
            // 根页标题（homepage_title）优先于二级页标题（android:id/title）
            foreach (var n in nodes)
            {
                if ((string?)n.Attribute("resource-id") is { } id
                    && id.EndsWith("homepage_title", StringComparison.Ordinal)
                    && ((string?)n.Attribute("text"))?.Trim() is { Length: > 0 } homeTitle)
                {
                    title = homeTitle;
                    source = "homepage_title";
                    break;
                }
            }
            if (title is null)
            {
                foreach (var n in nodes)
                {
                    if ((string?)n.Attribute("resource-id") == "android:id/title"
                        && ((string?)n.Attribute("text"))?.Trim() is { Length: > 0 } pageTitle)
                    {
                        title = pageTitle;
                        source = "title";
                        break;
                    }
                }
            }
            if (title is null)
                return "android.settings";
            var hasUp = nodes.Any(n =>
                (string?)n.Attribute("resource-id") == "android:id/up"
                || (string?)n.Attribute("content-desc") == "Navigate up");
            return $"android.settings|rk1:{title}|src={source}|up={(hasUp ? 1 : 0)}";
        }
        catch (Exception) when (xml.Length > 0)
        {
            return "android.settings";
        }
    }

    /// <summary>
    /// AGT-010 §2 — ViewportDigest（视口摘要，vd1 = XML 来源/算法版本）：
    /// 当前视口 clickable 元素（text ∥ resource-id 短 id）排序多重集的
    /// sha256 前 8 hex。与 RouteKey 字段集不相交、不得互相替代（§2 实施约束）：
    /// 同页滚动 → key 不变 digest 变；同视口重复观察 → digest 稳定（e1 语料
    /// 三视口成组实证）。本摘要只作可观测性（trace/测试/后续 OCR/Slow 来源
    /// 对齐）——滚动内容变化的判定权威仍是 Kernel post-action occurrence
    /// 集合比较，不建第二判断面。无 XML/不可解析 → null（无声明）。
    /// </summary>
    internal static string? DeriveViewportDigest(string? xml)
    {
        if (xml is null)
            return null;
        try
        {
            var root = XDocument.Parse(xml).Root;
            if (root is null)
                return null;
            var descriptors = root.Descendants("node")
                .Where(n => (string?)n.Attribute("clickable") == "true")
                .Select(n =>
                {
                    var text = ((string?)n.Attribute("text"))?.Trim();
                    if (!string.IsNullOrEmpty(text))
                        return text;
                    var rid = (string?)n.Attribute("resource-id");
                    return string.IsNullOrEmpty(rid) ? "??" : ShortId(rid!);
                })
                .OrderBy(d => d, StringComparer.Ordinal)
                .ToList();
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = Convert.ToHexString(sha.ComputeHash(
                System.Text.Encoding.UTF8.GetBytes(string.Join("|", descriptors))))[..8];
            return $"vd1:{hash}";
        }
        catch (Exception) when (xml.Length > 0)
        {
            return null;
        }
    }

    /// <summary>resource-id 短 id（最后一段 ':' 之后；无冒号 = 原值）。</summary>
    private static string ShortId(string resourceId)
    {
        var index = resourceId.LastIndexOf(':');
        return index < 0 ? resourceId : resourceId[(index + 1)..];
    }

    /// <summary>
    /// AGT-009 §1 — 确定性 XML 结构弹窗分类器。present 当且仅当 XML 可解析且
    ///（窗口根 package ≠ hostPackage ∨ 任一 node resource-id ∈ 弹窗白名单）；
    /// absent = 可解析且条件全不成立；xml 为 null 或不可解析 → null（无 XML
    /// ⇒ 无弹窗声明——绝不从 Fast/截图伪造结构事实）。几何不作判据。
    /// </summary>
    /// <returns>"present" | "absent" | null（无声明）</returns>
    internal static string? DerivePopupState(string? xml, string hostPackage) =>
        DerivePopupState(xml, hostPackage, SettingsCoverage.PopupClearanceConfig.DefaultPopupResourceIds);

    internal static string? DerivePopupState(string? xml, string hostPackage, IReadOnlyList<string> popupResourceIds)
    {
        if (xml is null)
            return null;
        XElement? root;
        try
        {
            root = XDocument.Parse(xml).Root;
        }
        catch (Exception)
        {
            return null; // 不可解析 → 无声明（不猜测）
        }
        if (root is null)
            return null;
        var nodes = root.Descendants("node").ToList();
        var windowPackage = (string?)nodes.FirstOrDefault()?.Attribute("package");
        if (windowPackage is not null && windowPackage != hostPackage)
            return "present";
        var whitelist = popupResourceIds.ToHashSet(StringComparer.Ordinal);
        var hasWhitelistedId = nodes
            .Select(n => (string?)n.Attribute("resource-id"))
            .Any(id => id is not null && whitelist.Contains(id));
        return hasWhitelistedId ? "present" : "absent";
    }

    /// <summary>
    /// AGT-009 — 弹窗 typed 声明 proposal（ui.overlay.popup = present/absent）。
    /// 与既有 route claim 同形（producer host.live.settings；scope/lineage 携带
    /// capture 与 observationCycleId）；popupState 为 null（无 XML）→ 不产 proposal。
    /// </summary>
    internal static ObservationProposal? PopupProposal(
        string? popupState, string captureId, string observationCycleId,
        DateTimeOffset captureTime, ObservationContext context) =>
        popupState is null
            ? null
            : new ObservationProposal(
                new ObservationClaim(ProductAssociationStrategy.PopupSubject, popupState),
                IngressKind.Observation,
                context,
                new Provenance("host.live.settings", captureTime,
                    $"scope:ui.overlay.popup:{observationCycleId}",
                    new[] { "real-device", "capture:" + captureId, "popup-classifier" }));

    /// <summary>AGT-009 — Fast 可视检测与 XML 结构点击数的确定性冲突谓词：
    /// fast 有检出而 hierarchy 零可点击节点（或反之）。仅在 XML 在场时判定
    ///（无 XML 由 NoXml 覆盖）。documented deterministic predicate。</summary>
    internal static string? DeriveSlowTrigger(
        bool hierarchyAvailable, bool fastAvailable, int clickableNodeCount,
        string routeKey, int popupPresentStreak, SlowTriggerConfig? slow)
    {
        if (!hierarchyAvailable)
            return "NoXml";
        if ((fastAvailable && clickableNodeCount == 0)
            || (!fastAvailable && clickableNodeCount > 0))
            return "StructuralVisualConflict";
        if (popupPresentStreak >= (slow?.PopupConsecutiveCycles ?? 2))
            return "PopupConsecutiveFailures";
        if (routeKey == "android.settings")
            return "SemanticUnclear";
        return null;
    }

    /// <summary>AGT-009 — trigger → SlowConsultationRequest（复用当前 Fast 上下文；
    /// 视觉触发且 config 允许时携带截图 raw artifact；Reason 指名触发词）。</summary>
    internal static SlowConsultationRequest BuildSlowRequest(
        string trigger, bool visual, byte[]? screenshot,
        string captureId, DateTimeOffset captureTime, string observationCycleId,
        FastTextBasis? fastBasis = null)
    {
        var popupTrigger = trigger == "PopupConsecutiveFailures";
        return new SlowConsultationRequest(
            RequestId: $"slow-{trigger}-{captureId}",
            Target: "settings-screen",
            ClaimSubject: popupTrigger ? ProductAssociationStrategy.PopupSubject
                : ProductAssociationStrategy.ScreenRouteSubject,
            ClaimField: popupTrigger ? "state" : "route",
            RequiresRawArtifact: visual,
            BuyerRef: "host.settings-coverage",
            Reason: $"slow-trigger:{trigger}",
            CaptureId: captureId,
            CaptureTimestamp: captureTime,
            ObservationCycleId: observationCycleId,
            EvidenceIds: Array.Empty<string>(),
            RawArtifact: visual ? screenshot : null,
            FastBasis: visual ? null : fastBasis);
    }

    /// <summary>AGT-009 — trace 摘要：trigger|status|projected=N[|late]。
    /// 超时/未配置 → Defer/Unknown 语义留痕，不做静默降级声明。</summary>
    internal static string FormatSlowTrace(string trigger, SlowConsultationOutcome outcome) =>
        $"{trigger}|{outcome.Status}|projected={outcome.ProjectedProposals}{(outcome.IsLate ? "|late" : "")}";

    private static int CountClickableNodes(string? xml)
    {
        if (xml is null)
            return 0;
        try
        {
            return XDocument.Parse(xml).Root?
                .Descendants("node")
                .Count(n => (string?)n.Attribute("clickable") == "true") ?? 0;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    /// <summary>连续 present 周期计数：absent 归零；无声明（无 XML）保持不变
    /// （结构事实不可得 ≠ 弹窗消失）。</summary>
    private int UpdatePopupStreak(string? popupState)
    {
        if (popupState == "present")
            _popupPresentStreak++;
        else if (popupState == "absent")
            _popupPresentStreak = 0;
        return _popupPresentStreak;
    }

    /// <summary>AGT-009 §8 — 有界 Slow 咨询：config 关闭/预算尽/kernel 不可得 →
    /// 不发起；否则 build request → Consult（有界等待，永不阻塞周期）；
    /// 超时/未配置 → trace + 继续（Defer/Unknown 语义）。本路径零 Effect。</summary>
    private string? ConsultSlowIfTriggered(
        string? trigger, string captureId, string observationCycleId, byte[] screenshot,
        bool fastAvailable, FastTextBasis? fastBasis = null)
    {
        if (trigger is null)
            return null;
        var slow = _coverageConfig?.SlowSettings;
        if (slow is not { Enabled: true } || _slowRequests >= slow.MaxRequestsPerRun)
            return $"{trigger}|Skipped";
        var kernel = KernelProvider?.Invoke();
        if (kernel is null)
            return $"{trigger}|Skipped";
        _slowRequests++;
        // Visual Slow is backed by this cycle's raw artifact and has no Fast
        // YOLO/OCR prerequisite. Fast availability only influences the trigger
        // classification; it must never gate the independent Visual path.
        var visual = slow.VisualEnabled;
        if (!visual && fastBasis is not null)
        {
            var sessionCorrelation = string.IsNullOrWhiteSpace(kernel.RunId)
                ? "host.settings-coverage"
                : kernel.RunId;
            fastBasis = fastBasis with { SessionCorrelation = sessionCorrelation };
        }
        var request = BuildSlowRequest(trigger, visual, screenshot, captureId,
            fastBasis?.CaptureTimestamp ?? _clock.Now, observationCycleId, fastBasis);
        var outcome = _slowConsult(request, kernel, false,
            TimeSpan.FromMilliseconds(slow.BoundedWaitMs));
        return FormatSlowTrace(trigger, outcome);
    }

    private FastTextBasis? TryFast(byte[] png, string captureId, string observationCycleId,
        DateTimeOffset captureTime)
    {
        try
        {
            var response = _vision.Analyze(png);
            using var json = JsonDocument.Parse(response);
            var root = json.RootElement;
            static IReadOnlyList<string> Read(JsonElement root, string name) =>
                root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
                    ? value.EnumerateArray().Select(x => x.ToString()).Where(x => x.Length > 0).ToArray()
                    : Array.Empty<string>();
            return new FastTextBasis(captureId, _assets.DeviceId, observationCycleId,
                Read(root, "yolo"), Read(root, "ocr"), captureTime);
        }
        catch (Exception)
        {
            // Preserve a typed negative observation so provider unavailability
            // remains distinguishable from a valid empty detection result.
            return new FastTextBasis(captureId, _assets.DeviceId, observationCycleId,
                Array.Empty<string>(), Array.Empty<string>(), captureTime,
                ProviderAvailable: false, IsFresh: true);
        }
    }

    private (string? Xml, bool Structural) TryDump(
        ObservationContext context, string observationCycleId, string captureId, int width, int height)
    {
        if (!_probe.ShouldProbe(_clock.Now))
            return (null, true);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var result = UiAutomatorDump.TryDumpToDevice(_assets.DeviceId);
            if (result.Xml is null)
            {
                if (result.IsStructuralFailure) _probe.MarkUnavailable(_clock.Now);
                else _probe.MarkTransient();
                continue;
            }
            return result;
        }
        return (null, true);
    }

    /// <summary>证据写盘（fail closed：追溯是显式保证，IO 失败即抛）。</summary>
    internal static void WriteEvidenceFile(string dir, string fileName, byte[] payload)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, fileName), payload);
    }

    public void Dispose() => _vision.Dispose();
}
