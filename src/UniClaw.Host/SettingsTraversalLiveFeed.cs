using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
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
        bool persistHierarchies = true)
    {
        _clock = clock;
        _assets = assets;
        _acquisition = new AdbScreenshotAcquisition(assets.DeviceId, "adb", () => clock.Now);
        _vision = new VisionServiceSession(assets.ProviderRoot, assets.PythonExecutable, assets.CacheRoot);
        _evidenceDir = evidenceDir;
        _persistScreenshots = persistScreenshots;
        _persistHierarchies = persistHierarchies;
    }

    public IReadOnlyList<TraceEntry> Trace => _trace;

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
            proposals.Count, fastLatency, hierarchyLatency, screenIdentity));
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
