using System.Text;
using UniClaw.Host;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>
/// AGT-005 — 确定性场景认证：模拟 Settings 世界（反应式 feed + dry-run
/// driver）贯穿 SettingsCoverageRunner 全链。证明：配置驱动的覆盖任务
/// （根页/全可见一级入口/滚动发现/二级页/返回/重复进入）在一次 Kernel run
/// 内完成；每步有 DecisionId/回执/路由前后/验证 checks 的完整 trace；
/// 偏离模型经有界纠正恢复；持续偏离 fail closed。
/// </summary>
public sealed class SettingsCoverageScenarioTests
{
    private const int Width = 1080;
    private const int Height = 1920;
    private const string RootRoute = "android.settings|route:Settings";
    private static readonly string[] VisiblePages =
    {
        "Network & internet", "Connected devices", "Apps", "Notifications",
        "Battery", "Storage", "Sound & vibration",
    };
    private static readonly string[] ScrolledPages =
    {
        "Battery", "Storage", "Sound & vibration", "Accessibility", "System",
    };
    private const string ScrollContainer = "com.android.settings:id/main_content_scrollable_container";

    /// <summary>
    /// 反应式模拟世界：维护 route/scroll 状态；feed 按状态出 XML 观察；
    /// driver 按坐标命中映射为 tap/swipe 世界迁移。
    /// </summary>
    private sealed class SimulatedSettingsWorld
    {
        private int _captureCounter;
        private int _scrollOffset;
        private int _transitionalCycles;
        private bool _popupArmed;
        private bool _popupActive;

        public string Route { get; private set; } = RootRoute;

        /// <summary>AGT-006：前 N 个周期返回无标题过渡屏（路由回退身份）。</summary>
        public void StartTransitional(int cycles)
        {
            _transitionalCycles = cycles;
            Route = "android.settings";
        }

        /// <summary>AGT-009：下一次进入二级页时弹 permissioncontroller 弹窗。</summary>
        public void ArmPopupOnNextSecondLevelEntry() => _popupArmed = true;

        /// <summary>AGT-009：弹窗是否在场（清障后应 false）。</summary>
        public bool PopupActive => _popupActive;

        public (string Descriptor, int X1, int Y1, int X2, int Y2)[] HitRegions =>
            (_popupActive
                ? new[]
                {
                    // 词汇优先序：CANCEL（button2）在 DISMISS（button1）之前
                    ("CANCEL", 540, 900, 900, 1000),
                    ("DISMISS", 180, 900, 540, 1000),
                }
                : Array.Empty<(string, int, int, int, int)>())
            .Concat(CurrentEntries
                .Select((descriptor, index) => (descriptor, 0, 260 + index * 150, Width, 260 + index * 150 + 140)))
            .Concat(new[] { ("Navigate up", 0, 40, 150, 190) })
            .ToArray()!;

        private string[] CurrentEntries => _scrollOffset == 0 ? VisiblePages : ScrolledPages;

        public string TapAt(int x, int y)
        {
            var region = HitRegions.FirstOrDefault(r => x >= r.X1 && x < r.X2 && y >= r.Y1 && y < r.Y2);
            if (region.Descriptor is null or "")
                return "miss";
            if (region.Descriptor is "CANCEL" or "DISMISS")
            {
                _popupActive = false;
                return region.Descriptor;
            }
            if (region.Descriptor == "Navigate up")
            {
                if (Route != RootRoute)
                    Route = RootRoute;
                return "Navigate up";
            }
            if (Route == RootRoute)
            {
                Route = $"android.settings|route:{region.Descriptor}";
                if (_popupArmed)
                {
                    // AGT-009：随进入二级页弹窗出现（permissioncontroller 覆盖层）
                    _popupArmed = false;
                    _popupActive = true;
                }
            }
            return region.Descriptor;
        }

        public bool ScrollUp() => Route == RootRoute && _scrollOffset++ == 0;

        public string BuildXml()
        {
            var builder = new StringBuilder();
            builder.Append("""<?xml version='1.0' encoding='UTF-8'?><hierarchy rotation="0">""");
            if (_popupActive)
            {
                // AGT-009 §1：弹窗结构事实——窗口根 package ≠ hostPackage
                //（permissioncontroller）∨ 命中 alertTitle/parentPanel/buttonPanel
                // 白名单；首个 node 即窗口根。几何不作判据。
                builder.Append(Node("android:id/parentPanel", "android.widget.LinearLayout",
                    text: null, clickable: false, bounds: "[120,700][960,1120]",
                    package: "com.android.permissioncontroller"));
                builder.Append(Node("android:id/alertTitle", "android.widget.TextView",
                    text: "Allow Settings to modify system settings?", clickable: false,
                    bounds: "[160,740][920,820]", package: "com.android.permissioncontroller"));
                builder.Append(Node("android:id/button2", "android.widget.Button",
                    text: "CANCEL", clickable: true, bounds: "[540,900][900,1000]",
                    package: "com.android.permissioncontroller"));
                builder.Append(Node("android:id/button1", "android.widget.Button",
                    text: "DISMISS", clickable: true, bounds: "[180,900][540,1000]",
                    package: "com.android.permissioncontroller"));
            }
            if (Route == RootRoute)
            {
                builder.Append(Node(ScrollContainer, "android.widget.ScrollView", text: null,
                    scrollable: true, clickable: false, bounds: "[0,200][1080,1800]"));
                builder.Append(Node("com.android.settings:id/homepage_title", "android.widget.TextView",
                    text: "Settings", clickable: false, bounds: "[40,80][1040,180]"));
                for (var i = 0; i < CurrentEntries.Length; i++)
                    builder.Append(Node("", "android.widget.LinearLayout", text: CurrentEntries[i],
                        clickable: true, bounds: $"[0,{260 + i * 150}][{Width},{260 + i * 150 + 140}]"));
            }
            else if (Route == "android.settings")
            {
                // 过渡屏：无标题（路由回退身份）、无可 grounding 的返回键。
                builder.Append(Node(ScrollContainer, "android.widget.ScrollView", text: null,
                    scrollable: true, clickable: false, bounds: "[0,200][1080,1800]"));
            }
            else
            {
                var title = Route["android.settings|route:".Length..];
                builder.Append(Node("android:id/up", "android.widget.ImageButton", text: null,
                    contentDesc: "Navigate up", clickable: true, bounds: "[0,40][150,190]"));
                builder.Append(Node("android:id/title", "android.widget.TextView", text: title,
                    clickable: false, bounds: "[200,80][1040,180]"));
                builder.Append(Node("com.android.settings:id/content_parent", "android.widget.ScrollView",
                    text: null, scrollable: true, clickable: false, bounds: "[0,250][1080,1800]"));
                builder.Append(Node("", "android.widget.LinearLayout", text: $"{title} content",
                    clickable: true, bounds: "[0,300][1080,440]"));
            }
            builder.Append("</hierarchy>");
            return builder.ToString();
        }

        public RunDriverInput? Next(ObservationDirective directive)
        {
            var captureId = $"sim-capture-{++_captureCounter:D4}";
            var xml = BuildXml();
            var route = SettingsTraversalLiveFeed.DeriveScreenIdentity(xml);
            // 过渡推进在本周期 XML 之后：当前周期如实呈现过渡屏，下一周期定形。
            if (_transitionalCycles > 0)
            {
                _transitionalCycles--;
                if (_transitionalCycles == 0)
                    Route = RootRoute;
            }
            var proposals = new List<ObservationProposal>
            {
                new(
                    new ObservationClaim(
                        UniClaw.Kernel.World.UiRealization.ProductAssociationStrategy.ScreenIdentitySubject,
                        "android.settings"),
                    IngressKind.Observation,
                    directive.Context,
                    new Provenance(
                        "host.sim.settings", DateTimeOffset.UtcNow, "scope:ui.screen",
                        new[] { "simulated-device", "capture:" + captureId })),
                new(
                    new ObservationClaim(
                        UniClaw.Kernel.World.UiRealization.ProductAssociationStrategy.ScreenRouteSubject,
                        route),
                    IngressKind.Observation,
                    directive.Context,
                    new Provenance(
                        "host.sim.settings", DateTimeOffset.UtcNow,
                        $"scope:ui.screen.route:sim-{_captureCounter:D4}",
                        new[] { "simulated-device", "capture:" + captureId, "route-fingerprint" })),
            };
            var parsed = UiAutomatorDump.ParseHierarchyObservation(
                xml,
                new UiAutomatorDump.UiHierarchyParseContext(
                    captureId, DateTimeOffset.UtcNow, "emulator-sim", "settings:sim", 35,
                    ObservationCycleId: $"sim-cycle-{_captureCounter:D4}",
                    Space: CoordinateSpace.DeviceViewport(Width, Height)));
            if (parsed.Observation is { } observation)
                proposals.AddRange(TypedHierarchyProposalProjector.Project(observation, directive.Context));
            // AGT-009：同分类器 typed 弹窗声明入证据流（ui.overlay.popup；
            // 无 XML/不可解析 → 无声明）。与真实 feed 同形。
            if (SettingsTraversalLiveFeed.PopupProposal(
                    SettingsTraversalLiveFeed.DerivePopupState(xml, "com.android.settings"),
                    captureId, $"sim-cycle-{_captureCounter:D4}", DateTimeOffset.UtcNow,
                    directive.Context)
                is { } popupProposal)
                proposals.Add(popupProposal);
            return new RunDriverInput.Observation(proposals);
        }

        public string CurrentCaptureId => $"sim-capture-{_captureCounter:D4}";

        private static string Node(
            string resourceId, string @class, string? text, bool clickable, string bounds,
            string? contentDesc = null, bool scrollable = false,
            string? package = null) =>
            $"""<node index="0" text="{Escape(text)}" resource-id="{Escape(resourceId)}" class="{Escape(@class)}" """
            + $"""package="{Escape(package ?? "com.android.settings")}" content-desc="{Escape(contentDesc)}" checkable="false" """
            + $"""checked="false" clickable="{(clickable ? "true" : "false")}" enabled="true" """
            + $"""focusable="true" focused="false" scrollable="{(scrollable ? "true" : "false")}" """
            + $"""long-clickable="false" password="false" selected="false" bounds="{Escape(bounds)}"/>""";

        private static string? Escape(string? value) =>
            value is null ? null
            : value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }

    private sealed class SimulatedEffectDriver(SimulatedSettingsWorld world) : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var locator = request.Target.Spatial
                ?? throw new InvalidOperationException("sim driver requires spatial target");
            var effect = request.EffectClass.ToLowerInvariant();
            if (effect is "swipe-up" or "swipe-down")
            {
                var changed = world.ScrollUp();
                return new DispatchResult(
                    DispatchOutcome.DeliveryCompleted,
                    $"sim {effect} (changed={changed})",
                    DateTimeOffset.UtcNow);
            }
            var x = (int)(locator.CenterX * Width);
            var y = (int)(locator.CenterY * Height);
            var hit = world.TapAt(x, y);
            return new DispatchResult(
                DispatchOutcome.DeliveryCompleted,
                $"sim tap {hit} @({x},{y})",
                DateTimeOffset.UtcNow);
        }
    }

    /// <summary>守规矩的确定性「模型」：按 directive 提案单步 Act。</summary>
    private static AgentDecision? ModelConsult(AgentDecisionContext context)
    {
        var marker = "COVERAGE DIRECTIVE (authoritative for this turn): ";
        var start = context.Objective.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            throw new InvalidOperationException("model double: directive missing");
        var directive = context.Objective[(start + marker.Length)..]
            .Split('\n', 2)[0].Trim();
        var target = ExtractTarget(directive);
        if (directive.StartsWith("Scroll", StringComparison.Ordinal))
            return Act(context, "scrollable", ScrollContainer, "swipe-up");
        return Act(context, "ui.element", target!, "tap");
    }

    /// <summary>
    /// AGT-009 §11 模型：obstacle directive 以受约束 advisory Plan 回答
    ///（恰一个 ActItem=词汇命中的清障 tap + ObserveItem(popup 补证)）；
    /// 其余 directive 照旧单步 Act。
    /// </summary>
    private static AgentDecision? ObstaclePlanModelConsult(AgentDecisionContext context)
    {
        var marker = "COVERAGE DIRECTIVE (authoritative for this turn): ";
        var start = context.Objective.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            throw new InvalidOperationException("model double: directive missing");
        var directive = context.Objective[(start + marker.Length)..]
            .Split('\n', 2)[0].Trim();
        if (directive.StartsWith("Clear the popup", StringComparison.Ordinal))
        {
            var target = ExtractTarget(directive);
            return new AgentDecision.Plan(context.DecisionId, new AgentPlanProposal(
                new PlanItem[]
                {
                    new PlanItem.ActItem("ui.element", target, "tap", null),
                    new PlanItem.ObserveItem(UniClaw.Kernel.World.UiRealization.ProductAssociationStrategy.PopupSubject),
                },
                Justification: "obstacle-clearance-plan"));
        }
        if (directive.StartsWith("Scroll", StringComparison.Ordinal))
            return Act(context, "scrollable", ScrollContainer, "swipe-up");
        return Act(context, "ui.element", ExtractTarget(directive)!, "tap");
    }

    private static readonly HashSet<string> DeviatedDecisionIds = new();

    private static AgentDecision? DeviatingModelConsult(AgentDecisionContext context)
    {
        if (!context.Objective.Contains("COVERAGE DIRECTIVE", StringComparison.Ordinal))
            return ModelConsult(context);
        // 同一 DecisionId 首次咨询偏离一次；纠正再咨询后守规矩。
        if (DeviatedDecisionIds.Add(context.DecisionId))
            return new AgentDecision.Act(new AgentActionProposal(
                context.DecisionId,
                new[] { new AgentActionStep("ui.element", "Storage", "tap", null) },
                Justification: "deviating-model"));
        return ModelConsult(context);
    }

    private static AgentDecision Act(
        AgentDecisionContext context, string role, string descriptor, string effect) =>
        new AgentDecision.Act(new AgentActionProposal(
            context.DecisionId,
            new[] { new AgentActionStep(role, descriptor, effect, null) },
            Justification: "model-double"));

    private static string? ExtractTarget(string directive)
    {
        var start = directive.IndexOf('<');
        var end = directive.IndexOf('>');
        return start >= 0 && end > start ? directive[(start + 1)..end] : null;
    }

    private static SettingsCoverageConfig ConfigWith(
        int maxSteps = 24, int maxScrolls = 4, int maxDirectiveRetries = 1) =>
        SettingsCoverageConfig.Load(WriteTempConfig(maxSteps, maxScrolls, maxDirectiveRetries));

    private static string WriteTempConfig(int maxSteps, int maxScrolls, int maxDirectiveRetries)
    {
        var path = Path.Combine(Path.GetTempPath(), $"settings-coverage-sim-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, $"""
            configVersion: "1"
            session:
              taskTitle: 遍历设置菜单覆盖测试
              workspace: UniClaw_Product_Tasks
              workspaceReuse: true
              autoCloseTurn: false
            bounds:
              maxSteps: {maxSteps}
              maxConsultRounds: 30
              maxScrolls: {maxScrolls}
              maxConsecutiveFailures: 3
              maxDirectiveRetries: {maxDirectiveRetries}
            coverage:
              rootPage: true
              firstLevelMode: all-visible
              scrollDiscoveredEntries: 1
              secondLevelPages: 2
              backNavigation: true
              repeatedEntries: 1
            targetPages:
              - Network & internet
              - Connected devices
              - Apps
              - Notifications
              - Battery
              - Storage
              - Sound & vibration
            termination:
              onCoverageComplete: true
              onMaxSteps: true
              onMaxScrolls: true
              onConsecutiveFailures: true
            rootRoute: android.settings|route:Settings
            scrollContainerDescriptor: {ScrollContainer}
            backDescriptor: Navigate up
            """);
        return path;
    }

    private static SettingsCoverageRunner.RunResult RunScenario(
        Func<AgentDecisionContext, AgentDecision?> model,
        SettingsCoverageConfig? config = null)
    {
        var world = new SimulatedSettingsWorld();
        var assets = new LivePerception.LiveAssets(
            "emulator-sim", "wifi-settings", "/nonexistent", "/nonexistent/python", "/nonexistent/cache");
        var runRoot = Path.Combine(Path.GetTempPath(), $"settings-coverage-run-{Guid.NewGuid():N}");
        return SettingsCoverageRunner.Run(runRoot, new SettingsCoverageRunner.Options(
            DeviceId: "emulator-sim",
            Live: assets,
            Config: config ?? ConfigWith(),
            UnderlyingConsult: model,
            DshSessionIdAccessor: () => "session-sim-0001",
            FeedNext: world.Next,
            CurrentCaptureId: () => world.CurrentCaptureId,
            EffectDriver: new SimulatedEffectDriver(world)));
    }

    [Fact]
    public void WriteEvidenceFile_PersistsPayloadUnderCaptureId()
    {
        // AGT-008：证据文件名 = captureId（与步骤 postCaptureId 直接关联）。
        var dir = Path.Combine(Path.GetTempPath(), $"settings-evidence-{Guid.NewGuid():N}");
        try
        {
            SettingsTraversalLiveFeed.WriteEvidenceFile(dir, "capture-abc.png", new byte[] { 1, 2, 3 });
            SettingsTraversalLiveFeed.WriteEvidenceFile(dir, "capture-abc.xml",
                System.Text.Encoding.UTF8.GetBytes("<hierarchy/>"));
            Assert.True(File.Exists(Path.Combine(dir, "capture-abc.png")));
            Assert.Equal(3, new FileInfo(Path.Combine(dir, "capture-abc.png")).Length);
            Assert.Contains("<hierarchy/>", File.ReadAllText(Path.Combine(dir, "capture-abc.xml")));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TransitionalStartupScreen_DefersUntilSettled_ThenCompletes()
    {
        // AGT-006：首个观察为无标题过渡屏（路由回退身份）→ director Defer
        // 重观察而非指挥幻影 back；定形后任务照常完成。
        var world = new SimulatedSettingsWorld();
        world.StartTransitional(1);
        var assets = new LivePerception.LiveAssets(
            "emulator-sim", "wifi-settings", "/nonexistent", "/nonexistent/python", "/nonexistent/cache");
        var runRoot = Path.Combine(Path.GetTempPath(), $"settings-coverage-run-{Guid.NewGuid():N}");
        var result = SettingsCoverageRunner.Run(runRoot, new SettingsCoverageRunner.Options(
            DeviceId: "emulator-sim",
            Live: assets,
            Config: ConfigWith(),
            UnderlyingConsult: ModelConsult,
            FeedNext: world.Next,
            CurrentCaptureId: () => world.CurrentCaptureId,
            EffectDriver: new SimulatedEffectDriver(world)));

        Assert.True(result.Status == RunDriveStatus.Completed,
            $"{result.Status}({result.Reason}) divergence={result.Report.FirstDivergence}");
        Assert.Equal("CoverageComplete", result.Report.Status);
        Assert.Contains(result.Consults, c => c.DirectiveKind == "defer-unknown-page");
    }

    /// <summary>
    /// AGT-009 验收 7（Drive 级闭环）：遍历中途弹窗（permissioncontroller）→
    /// director obstacle 分支发出清障 directive → 模型以受约束 advisory Plan
    /// 回答（ActItem=词汇命中的 CANCEL tap + ObserveItem(popup 补证)）→
    /// Kernel 逐项执行：ActItem 经真实 grounding/Gate/dispatch（清障 tap 落地、
    /// 弹窗消失）→ ObserveItem/剩余计划由失效规则处置 → popup 声明转 absent
    /// → 普通遍历恢复并完成覆盖。
    /// </summary>
    [Fact]
    public void PopupEpisode_ObstaclePlanClearsPopup_AndResumesTraversalToCompletion()
    {
        var world = new SimulatedSettingsWorld();
        world.ArmPopupOnNextSecondLevelEntry();
        var assets = new LivePerception.LiveAssets(
            "emulator-sim", "wifi-settings", "/nonexistent", "/nonexistent/python", "/nonexistent/cache");
        var runRoot = Path.Combine(Path.GetTempPath(), $"settings-coverage-run-{Guid.NewGuid():N}");
        var result = SettingsCoverageRunner.Run(runRoot, new SettingsCoverageRunner.Options(
            DeviceId: "emulator-sim",
            Live: assets,
            Config: ConfigWith(),
            UnderlyingConsult: ObstaclePlanModelConsult,
            DshSessionIdAccessor: () => "session-sim-0001",
            FeedNext: world.Next,
            CurrentCaptureId: () => world.CurrentCaptureId,
            EffectDriver: new SimulatedEffectDriver(world)));

        // 闭环终点：弹窗已清（同分类器前后测：present → absent）且任务完成
        Assert.True(result.Status == RunDriveStatus.Completed,
            $"{result.Status}({result.Reason}) divergence={result.Report.FirstDivergence}");
        Assert.Equal("CoverageComplete", result.Report.Status);
        Assert.False(world.PopupActive);

        // obstacle 分支确实发生，且回答形态是受约束 advisory Plan
        var obstacle = result.Consults.Single(c => c.DirectiveKind == "obstacle");
        Assert.Equal("plan", obstacle.DecisionKind);
        Assert.Null(obstacle.DeviationReason);
        Assert.Contains("CANCEL", obstacle.Directive);

        // ActItem 经真实执行链落地：清障步骤有回执（click 型目标消失且路由
        // 未变——通用 target-unique check 如实失败，弹窗清障本身由
        // ui.overlay.popup present→absent 的 typed 声明证明；剩余计划按
        // 冻结决策 11 由验证失败废弃）
        var clearance = result.Steps.Single(s => s.TargetDescriptor == "CANCEL");
        Assert.Equal("DeliveryCompleted", clearance.ReceiptOutcome);
        Assert.Equal("obstacle-clearance-plan", obstacle.Justification);

        // 清障后普通遍历恢复：obstacle 咨询之后仍有正常 enter/scroll 指令
        var consultList = result.Consults.ToList();
        var obstacleIndex = consultList.IndexOf(obstacle);
        Assert.Contains(consultList.Skip(obstacleIndex + 1),
            c => c.DirectiveKind is "enter" or "re-enter" or "scroll" or "back");
        // 全部覆盖项最终完成（清障插曲不丢失任务）
        Assert.Empty(result.Report.UncoveredItems);
    }

    [Fact]
    public void PersistentUnknownPage_StopsHonestlyAfterDeferBudget()
    {
        // 过渡屏永不定形 → Defer 配额尽 → bounded-stop:unknown-page（fail closed）。
        var world = new SimulatedSettingsWorld();
        world.StartTransitional(999);
        var assets = new LivePerception.LiveAssets(
            "emulator-sim", "wifi-settings", "/nonexistent", "/nonexistent/python", "/nonexistent/cache");
        var runRoot = Path.Combine(Path.GetTempPath(), $"settings-coverage-run-{Guid.NewGuid():N}");
        var result = SettingsCoverageRunner.Run(runRoot, new SettingsCoverageRunner.Options(
            DeviceId: "emulator-sim",
            Live: assets,
            Config: ConfigWith(),
            UnderlyingConsult: ModelConsult,
            FeedNext: world.Next,
            CurrentCaptureId: () => world.CurrentCaptureId,
            EffectDriver: new SimulatedEffectDriver(world)));

        Assert.Equal("bounded-stop:unknown-page", result.Consults[^1].Justification);
        // 零 dispatch：未知页上从未发出任何 effect。
        Assert.Empty(result.Steps);
    }

    [Fact]
    public void FullCoverageMission_CompletesWithTraceableSteps()
    {
        var result = RunScenario(ModelConsult);

        Assert.True(result.Status == RunDriveStatus.Completed,
            $"{result.Status}({result.Reason}) divergence={result.Report.FirstDivergence}");
        Assert.Equal("Completion", result.Outcome);
        Assert.Equal("CoverageComplete", result.Report.Status);
        Assert.Equal(1.0, result.Report.CoverageRate, 5);
        Assert.Equal(1.0, result.Report.StepSuccessRate, 5);
        Assert.Empty(result.Report.UncoveredItems);
        Assert.Null(result.Report.FirstDivergence);
        Assert.All(result.Report.Items, item => Assert.True(item.Covered, item.Requirement));

        // 步骤序列：7×(enter+back) + scroll + (scroll-entry+back) + (re-enter+back) = 19。
        Assert.Equal(19, result.Steps.Count);
        Assert.Equal(19, result.Steps.Count(s => s.Verified));
        Assert.All(result.Steps, s =>
        {
            Assert.NotNull(s.ReceiptId);
            Assert.Equal("DeliveryCompleted", s.ReceiptOutcome);
            Assert.True(s.PreActionTargetUnique, $"step {s.Index} target not unique");
            Assert.NotNull(s.RouteBefore);
            Assert.NotNull(s.RouteAfter);
            Assert.NotNull(s.PostCaptureId);
            Assert.Equal("session-sim-0001", s.DshSessionId);
            Assert.Contains(new KeyValuePair<string, bool>("post-action-target-unique", true), s.Checks);
        });

        // 滚动步骤：路由未变 + 内容变化 checks。
        var scroll = result.Steps.Single(s => s.EffectClass == "swipe-up");
        Assert.Equal(RootRoute, scroll.RouteBefore);
        Assert.Equal(RootRoute, scroll.RouteAfter);
        Assert.Contains(new KeyValuePair<string, bool>("post-action-route-unchanged", true), scroll.Checks);
        Assert.Contains(new KeyValuePair<string, bool>("post-action-content-transition", true), scroll.Checks);

        // 返回步骤：路由回到根页；重复进入：同 descriptor 两次、不同回执。
        var backs = result.Steps.Where(s => s.TargetDescriptor == "Navigate up").ToList();
        Assert.Equal(9, backs.Count);
        Assert.All(backs, b => Assert.Equal(RootRoute, b.RouteAfter));
        var batteryEntries = result.Steps.Where(s => s.TargetDescriptor == "Battery").ToList();
        Assert.Equal(2, batteryEntries.Count);
        Assert.NotEqual(batteryEntries[0].ReceiptId, batteryEntries[1].ReceiptId);

        // 工件落盘。
        Assert.True(File.Exists(Path.Combine(result.RunDir, "coverage-steps.json")));
        Assert.True(File.Exists(Path.Combine(result.RunDir, "coverage-report.json")));
        Assert.True(File.Exists(Path.Combine(result.RunDir, "facts.json")));
        Assert.True(File.Exists(Path.Combine(result.RunDir, "trace.json")));
    }

    [Fact]
    public void MaxStepsBound_StopsHonestlyWithUncoveredItems()
    {
        // maxSteps=6：进入 3 个入口后停止（bounded stop，不伪装完成）。
        var result = RunScenario(ModelConsult, ConfigWith(maxSteps: 6));

        Assert.Equal("BoundedStop", result.Report.Status);
        Assert.True(result.Report.CoverageRate < 1.0);
        Assert.NotEmpty(result.Report.UncoveredItems);
        Assert.Equal(6, result.Steps.Count);
    }

    [Fact]
    public void DeviatingModel_RecoversViaBoundedCorrectiveReconsult()
    {
        var result = RunScenario(DeviatingModelConsult);

        // 首轮偏离被纠正再咨询救回；任务仍完成。
        Assert.True(result.Status == RunDriveStatus.Completed,
            $"{result.Status}({result.Reason}) divergence={result.Report.FirstDivergence}");
        Assert.Equal("CoverageComplete", result.Report.Status);
        var first = result.Consults.First();
        Assert.Equal(2, first.Attempts);
        Assert.NotNull(first.DeviationReason);
        Assert.Contains("directive-target-mismatch", first.DeviationReason);
    }

    [Fact]
    public void PersistentlyDeviatingModel_FailsClosed()
    {
        // 零纠正预算 + 恒定偏离 → fail closed，且偏离留痕。
        AgentDecision? PersistentDeviation(AgentDecisionContext context) =>
            Act(context, "ui.element", "Storage", "tap");

        var result = RunScenario(PersistentDeviation, ConfigWith(maxDirectiveRetries: 0));

        Assert.True(result.Status == RunDriveStatus.AgentDecisionFailed,
            $"{result.Status}({result.Reason})");
        Assert.Contains(result.Consults, c => c.DeviationReason is not null);
        // 零 dispatch：偏离步骤不产生回执。
        Assert.Empty(result.Steps);
    }

    [Fact]
    public void IllegalDesiredStateOnDirectiveStep_IsRejectedByDirector()
    {
        AgentDecision? IllegalState(AgentDecisionContext context) =>
            new AgentDecision.Act(new AgentActionProposal(
                context.DecisionId,
                new[] { new AgentActionStep("ui.element", "Network & internet", "tap", "mostly-on") },
                Justification: "illegal-state"));

        var result = RunScenario(IllegalState, ConfigWith(maxDirectiveRetries: 0));

        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Contains(result.Consults,
            c => c.DeviationReason == "directive-step-must-not-carry-desiredState");
        Assert.Empty(result.Steps);
    }
}
