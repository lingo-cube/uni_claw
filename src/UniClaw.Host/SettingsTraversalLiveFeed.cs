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
        string? Grounding = null,
        string? Assurance = null,
        string? Effect = null,
        string? Verification = null);

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
    private Func<SlowConsultationRequest, UniKernel, bool, TimeSpan?, SlowConsultationOutcome> _slowConsult;
    private int _popupPresentStreak;
    private int _slowRequests;
    private int _cycle;

    public SettingsTraversalLiveFeed(HostUtilities.VirtualClock clock, LivePerception.LiveAssets assets)
        : this(clock, assets, null)
    {
    }

    /// <summary>AGT-008：evidenceDir 非 null 时每周期落盘 {captureId}.png/.xml
    /// （文件名与步骤记录 postCaptureId 直接关联；写失败 fail closed）。</summary>
    public SettingsTraversalLiveFeed(
        HostUtilities.VirtualClock clock,
        LivePerception.LiveAssets assets,
        string? evidenceDir,
        bool persistScreenshots = true,
        bool persistHierarchies = true,
        SettingsCoverageConfig? coverageConfig = null)
    {
        _clock = clock;
        _assets = assets;
        _acquisition = new AdbScreenshotAcquisition(assets.DeviceId, "adb", () => clock.Now);
        _vision = new VisionServiceSession(assets.ProviderRoot, assets.PythonExecutable, assets.CacheRoot);
        _evidenceDir = evidenceDir;
        _persistScreenshots = persistScreenshots;
        _persistHierarchies = persistHierarchies;
        _coverageConfig = coverageConfig;
        _slowConsult = (request, kernel, effectCritical, boundedWait) =>
            new SlowConsultation().Consult(request, kernel, effectCritical, boundedWait);
    }

    public IReadOnlyList<TraceEntry> Trace => _trace;

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
        var fastAvailable = TryFast(capture.Artifact.Payload);
        var fastLatency = Stopwatch.GetElapsedTime(fastStart);

        var hierarchyStart = Stopwatch.GetTimestamp();
        var xmlResult = TryDump(directive.Context, observationCycleId, captureId,
            capture.Width, capture.Height);
        var hierarchyLatency = Stopwatch.GetElapsedTime(hierarchyStart);
        var screenIdentity = xmlResult.Xml is null
            ? "android.settings"
            : DeriveScreenIdentity(xmlResult.Xml);
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
            new ObservationClaim(ProductAssociationStrategy.ScreenRouteSubject, screenIdentity),
            IngressKind.Observation,
            directive.Context,
            new Provenance("host.live.settings", _clock.Now,
                $"scope:ui.screen.route:{observationCycleId}",
                new[] { "real-device", "capture:" + captureId, "route-fingerprint" })));
        var popupProposal = PopupProposal(popupState, captureId, observationCycleId, _clock.Now, directive.Context);
        if (popupProposal is not null)
            proposals.Add(popupProposal);

        // AGT-009 §8：有界 Slow 触发（默认关闭；预算/超时/config 三重收口）。
        var slowTrace = ConsultSlowIfTriggered(
            trigger: DeriveSlowTrigger(
                xmlResult.Xml is not null, fastAvailable, CountClickableNodes(xmlResult.Xml), screenIdentity,
                UpdatePopupStreak(popupState), _coverageConfig?.SlowSettings),
            captureId, observationCycleId, capture.Artifact.Payload, fastAvailable);
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
                        Space: CoordinateSpace.DeviceViewport(capture.Width, capture.Height)));
                if (typed.Observation is { } observation)
                    proposals.AddRange(TypedHierarchyProposalProjector.Project(observation, directive.Context));
            }
        }

        _trace.Add(new TraceEntry(cycle, directive.Context.ToString(), captureId,
            observationCycleId, fastAvailable, xmlResult.Xml is not null,
            proposals.Count, fastLatency, hierarchyLatency, screenIdentity, popupState, slowTrace));
        return new RunDriverInput.Observation(proposals);
    }

    /// <summary>
    /// Settings 的页面标题是当前产品实现中唯一稳定、可观测的 route
    /// 身份。它只用于 WorldModel container association；不参与 target
    /// grounding，也不制造坐标或 resource-id。若标题缺失，保留原有
    /// android.settings 身份，导航验证会继续 fail-closed。
    /// </summary>
    internal static string DeriveScreenIdentity(string xml)
    {
        try
        {
            var root = XDocument.Parse(xml).Root;
            if (root is null)
                return "android.settings";
            var nodes = root.Descendants("node");
            var title = nodes
                .Where(n => (string?)n.Attribute("resource-id") is { } id
                    && id.EndsWith("homepage_title", StringComparison.Ordinal))
                .Select(n => (string?)n.Attribute("text"))
                .Concat(nodes
                    .Where(n => (string?)n.Attribute("resource-id") == "android:id/title")
                    .Select(n => (string?)n.Attribute("text")))
                .Select(value => value?.Trim())
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            return string.IsNullOrWhiteSpace(title)
                ? "android.settings"
                : $"android.settings|route:{title}";
        }
        catch (Exception) when (xml.Length > 0)
        {
            return "android.settings";
        }
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
        string screenIdentity, int popupPresentStreak, SlowTriggerConfig? slow)
    {
        if (!hierarchyAvailable)
            return "NoXml";
        if ((fastAvailable && clickableNodeCount == 0)
            || (!fastAvailable && clickableNodeCount > 0))
            return "StructuralVisualConflict";
        if (popupPresentStreak >= (slow?.PopupConsecutiveCycles ?? 2))
            return "PopupConsecutiveFailures";
        if (screenIdentity == "android.settings")
            return "SemanticUnclear";
        return null;
    }

    /// <summary>AGT-009 — trigger → SlowConsultationRequest（复用当前 Fast 上下文；
    /// 视觉触发且 config 允许时携带截图 raw artifact；Reason 指名触发词）。</summary>
    internal static SlowConsultationRequest BuildSlowRequest(
        string trigger, bool visual, byte[]? screenshot,
        string captureId, DateTimeOffset captureTime, string observationCycleId)
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
            RawArtifact: visual ? screenshot : null);
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
        string? trigger, string captureId, string observationCycleId, byte[] screenshot, bool fastAvailable)
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
        var visual = slow.VisualEnabled && fastAvailable;
        var request = BuildSlowRequest(trigger, visual, screenshot, captureId, _clock.Now, observationCycleId);
        var outcome = _slowConsult(request, kernel, false,
            TimeSpan.FromMilliseconds(slow.BoundedWaitMs));
        return FormatSlowTrace(trigger, outcome);
    }

    private bool TryFast(byte[] png)
    {
        try
        {
            var response = _vision.Analyze(png);
            using var json = JsonDocument.Parse(response);
            return json.RootElement.TryGetProperty("yolo", out var yolo)
                && yolo.ValueKind == JsonValueKind.Array
                && yolo.GetArrayLength() > 0;
        }
        catch (Exception)
        {
            return false;
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
