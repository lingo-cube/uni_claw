using System.Collections.Immutable;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Observability;
using UniClaw.Core.Observation;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.Observation;

/// <summary>
/// ObservationPipeline 三级级联测试 (core-observation-pipeline D1/D2/D6)。
/// 纯仿真：无 emulator、无 AI 服务。覆盖：
///   L1 UIA 足够 → UIA-only，零 AI 调用，trace decision "UIA"；
///   L1 不足 / 弹窗 → 回落 AI (decision "AI")；
///   dump 失败 / UIA_Enabled=false / 能力不可用 → 直连 AI (decision "UIA_disabled")；
///   AI 空响应 → DomainValidationException 直抛 (不重试、不回退 UIA)；
///   SkipUIAOnBackNavigation → back 后复用缓存分析 (零 dump 零 AI)。
/// </summary>
public sealed class ObservationPipelineTests
{
    private const string HomeXml =
        """
        <hierarchy>
          <node text="Settings" resource-id="com.android.settings:id/homepage_title" clickable="false" bounds="[0,0][1080,180]" />
          <node text="Network &amp; internet" class="android.widget.TextView" clickable="true" bounds="[0,200][1080,380]" />
          <node text="Connected devices" class="android.widget.TextView" clickable="true" bounds="[0,400][1080,580]" />
          <node text="Apps" class="android.widget.TextView" clickable="true" bounds="[0,600][1080,780]" />
        </hierarchy>
        """;

    private const string PopupXml =
        """
        <hierarchy>
          <node text="Settings" resource-id="com.android.settings:id/homepage_title" clickable="false" bounds="[0,0][1080,180]" />
          <node text="Deny" class="android.widget.TextView" clickable="true" bounds="[0,200][1080,380]" />
          <node text="Allow" class="android.widget.TextView" clickable="true" bounds="[0,400][1080,580]" />
          <node text="Apps" class="android.widget.TextView" clickable="true" bounds="[0,600][1080,780]" />
        </hierarchy>
        """;

    private const string SingleItemXml =
        """
        <hierarchy>
          <node text="Settings" resource-id="com.android.settings:id/homepage_title" clickable="false" bounds="[0,0][1080,180]" />
          <node text="About emulated device" class="android.widget.TextView" clickable="true" bounds="[0,1500][1080,1800]" />
        </hierarchy>
        """;

    private const string SubPageXml =
        """
        <hierarchy>
          <node text="Network &amp; internet" resource-id="com.android.settings:id/collapsing_toolbar" clickable="false" bounds="[0,0][1080,180]" />
          <node text="Wi-Fi" class="android.widget.TextView" clickable="true" bounds="[0,200][1080,380]" />
          <node text="Mobile network" class="android.widget.TextView" clickable="true" bounds="[0,400][1080,580]" />
          <node text="Hotspot" class="android.widget.TextView" clickable="true" bounds="[0,600][1080,780]" />
        </hierarchy>
        """;

    // ── 1. L1 UIA 足够 → UIA-only ────────────────────────────────────────

    [Fact(DisplayName = "D1: UIA dump 成功且 ≥N items 且无弹窗 → 返回 UIA-only，零 AI 调用")]
    public async Task Uia_SufficientItems_ReturnsUiaOnly_NoAiCall()
    {
        var state = ScriptedScreenState.Succeed(HomeXml, "fp-home");
        var visual = new FakeVisualAnalyzer();
        var trace = NewTrace(out var storage);

        var pipeline = NewPipeline(state, visual, trace: trace);

        var analysis = await pipeline.AnalyzeCurrentPageAsync();

        Assert.NotNull(analysis);
        Assert.Contains(analysis!.Items, item => item.Name == "Apps");
        Assert.Equal(0, visual.Calls);
        Assert.Equal(1, state.RefreshCount);
        Assert.Contains(
            storage.GetExecutions(),
            record => record.Action == "UIA");
    }

    // ── 2. L1 不足 → 回落 AI ─────────────────────────────────────────────

    [Fact(DisplayName = "D1: UIA dump 成功但 <N items → 回落 AI，trace decision 'AI'")]
    public async Task Uia_TooFewItems_FallsThroughToAi()
    {
        var state = ScriptedScreenState.Succeed(SingleItemXml, "fp-sparse");
        var visual = new FakeVisualAnalyzer();
        var trace = NewTrace(out var storage);

        var pipeline = NewPipeline(state, visual, trace: trace);

        var analysis = await pipeline.AnalyzeCurrentPageAsync();

        Assert.Equal("AI Page", analysis!.CurrentPath.Single());
        Assert.Equal(1, visual.Calls);
        Assert.Contains(
            storage.GetExecutions(),
            record => record.Action == "AI");
        Assert.DoesNotContain(
            storage.GetExecutions(),
            record => record.Action == "UIA");
    }

    [Fact(DisplayName = "D1: UIA items 含弹窗按钮 → 回落 AI（弹窗场景 UIA 无语义）")]
    public async Task Uia_PopupItems_FallThroughToAi()
    {
        var state = ScriptedScreenState.Succeed(PopupXml, "fp-popup");
        var visual = new FakeVisualAnalyzer();
        var trace = NewTrace(out var storage);

        var pipeline = NewPipeline(state, visual, trace: trace);

        var analysis = await pipeline.AnalyzeCurrentPageAsync();

        Assert.Equal("AI Page", analysis!.CurrentPath.Single());
        Assert.Equal(1, visual.Calls);
        Assert.Contains(
            storage.GetExecutions(),
            record => record.Action == "AI");
    }

    [Fact(DisplayName = "Spec: EnablePopupDetection=false → 弹窗启发式不触发 AI 回落")]
    public async Task PopupDetectionDisabled_KeepsUiaOnly()
    {
        var state = ScriptedScreenState.Succeed(PopupXml, "fp-popup");
        var visual = new FakeVisualAnalyzer();

        var pipeline = NewPipeline(
            state,
            visual,
            config: new ObservationConfig(EnablePopupDetection: false));

        var analysis = await pipeline.AnalyzeCurrentPageAsync();

        Assert.Contains(analysis!.Items, item => item.Name == "Deny");
        Assert.Equal(0, visual.Calls);
    }

    [Fact(DisplayName = "Spec: UIA_MinItems=5 → 仅 3 项时跳过 UIA 回落 AI")]
    public async Task Uia_MinItems5_SkipsThreeItemPage()
    {
        var state = ScriptedScreenState.Succeed(HomeXml, "fp-home");
        var visual = new FakeVisualAnalyzer();

        var pipeline = NewPipeline(
            state,
            visual,
            config: new ObservationConfig(UIA_MinItems: 5));

        var analysis = await pipeline.AnalyzeCurrentPageAsync();

        Assert.Equal("AI Page", analysis!.CurrentPath.Single());
        Assert.Equal(1, visual.Calls);
    }

    // ── 3. dump 失败 → AI directly ───────────────────────────────────────

    [Fact(DisplayName = "D1: UIA dump Succeeded=false → 直连 AI，不做 UIA 解析")]
    public async Task Uia_DumpFailed_AiDirectly()
    {
        var state = new ScriptedScreenState(
            new ScreenStateResult(
                false,
                "adb_failure",
                null,
                string.Empty,
                false,
                false,
                new ScreenFailure("non_zero_exit", "device offline")));
        var visual = new FakeVisualAnalyzer();

        var pipeline = NewPipeline(state, visual);

        var analysis = await pipeline.AnalyzeCurrentPageAsync();

        Assert.Equal("AI Page", analysis!.CurrentPath.Single());
        Assert.Equal(1, visual.Calls);
        Assert.Equal(1, state.RefreshCount);
    }

    [Fact(DisplayName = "D1: UIA dump 成功但 HierarchyXml 为空 → 直连 AI")]
    public async Task Uia_EmptyHierarchyXml_AiDirectly()
    {
        var state = new ScriptedScreenState(
            new ScreenStateResult(
                true,
                "ok",
                string.Empty,
                "fp",
                false,
                false,
                null));
        var visual = new FakeVisualAnalyzer();

        var pipeline = NewPipeline(state, visual);

        var analysis = await pipeline.AnalyzeCurrentPageAsync();

        Assert.Equal("AI Page", analysis!.CurrentPath.Single());
        Assert.Equal(1, visual.Calls);
    }

    // ── 4. UIA 关闭 / 能力不可用 → 跳过 L1 ───────────────────────────────

    [Fact(DisplayName = "D6: UIA_Enabled=false → 跳过 L1 直连 AI，trace decision 'UIA_disabled'")]
    public async Task Uia_DisabledByConfig_AiDirectly_UiaDisabledDecision()
    {
        var state = ScriptedScreenState.Succeed(HomeXml, "fp-home");
        var visual = new FakeVisualAnalyzer();
        var trace = NewTrace(out var storage);

        var pipeline = NewPipeline(
            state,
            visual,
            config: new ObservationConfig(UIA_Enabled: false),
            trace: trace);

        var analysis = await pipeline.AnalyzeCurrentPageAsync();

        Assert.Equal("AI Page", analysis!.CurrentPath.Single());
        Assert.Equal(0, state.RefreshCount); // L1 entirely skipped — no dump
        Assert.Contains(
            storage.GetExecutions(),
            record => record.Action == "UIA_disabled");
    }

    [Fact(DisplayName = "AC5: 设备能力 UIA_Available=false → 后续直接 AI，跳过 L1")]
    public async Task Uia_DeviceCapabilityUnavailable_SkipsL1()
    {
        var state = ScriptedScreenState.Succeed(HomeXml, "fp-home");
        state.IsUiAutomatorAvailable = false;
        var visual = new FakeVisualAnalyzer();
        var trace = NewTrace(out var storage);

        var pipeline = NewPipeline(state, visual, trace: trace);

        var analysis = await pipeline.AnalyzeCurrentPageAsync();

        Assert.Equal("AI Page", analysis!.CurrentPath.Single());
        Assert.Equal(0, state.RefreshCount);
        Assert.Contains(
            storage.GetExecutions(),
            record => record.Action == "UIA_disabled");
    }

    // ── 5. AI 空响应 → 快速失败 ──────────────────────────────────────────

    [Fact(DisplayName = "AC4: AI 空响应 → DomainValidationException 直抛，不重试不回退 UIA")]
    public async Task Ai_EmptyResponse_ThrowsDomainValidation()
    {
        // SingleItemXml (< UIA_MinItems) so the pipeline falls through to AI.
        var state = ScriptedScreenState.Succeed(SingleItemXml, "fp-sparse");
        var visual = new FakeVisualAnalyzer
        {
            Exception = new DomainValidationException(
                "content",
                null,
                "analyze_visual model returned empty response — structural failure, will not retry."),
        };

        var pipeline = NewPipeline(state, visual);

        var exception = await Assert.ThrowsAsync<DomainValidationException>(
            () => pipeline.AnalyzeCurrentPageAsync());

        Assert.Contains("empty response", exception.Message);
        Assert.Equal(1, visual.Calls); // exactly one attempt — no retry
    }

    // ── 6. SkipUIAOnBackNavigation ───────────────────────────────────────

    [Fact(DisplayName = "AC6: back 导航后复用缓存分析 — 零 dump 零 AI")]
    public async Task BackNavigation_ReusesCachedAnalysis_NoDumpNoAi()
    {
        var state = new ScriptedScreenState(
            ScreenStateResult(HomeXml, "fp-home"),
            ScreenStateResult(SubPageXml, "fp-sub"));
        var visual = new FakeVisualAnalyzer();
        var trace = NewTrace(out var storage);

        var pipeline = NewPipeline(state, visual, trace: trace);

        var home = await pipeline.AnalyzeCurrentPageAsync();     // UIA-only (home)
        var sub = await pipeline.AnalyzeCurrentPageAsync();      // UIA-only (sub)
        Assert.Equal(2, state.RefreshCount);
        Assert.NotEqual(home!.CurrentPath, sub!.CurrentPath);

        pipeline.MarkBackNavigation();
        var after = await pipeline.AnalyzeCurrentPageAsync();    // reuse cached home

        Assert.Same(home, after);
        Assert.Equal(2, state.RefreshCount); // no additional dump
        Assert.Equal(0, visual.Calls);       // no AI call
        Assert.Contains(
            storage.GetExecutions(),
            record => record.Action == "UIA_back_reuse");
    }

    [Fact(DisplayName = "D2: back 复用无历史页面 → 回落正常分析")]
    public async Task BackNavigation_NoPriorPage_FallsBackToNormalAnalysis()
    {
        // Two queued results: the fallback path re-observes after the back
        // marker is consumed with no earlier distinct page to reuse.
        var state = new ScriptedScreenState(
            ScreenStateResult(HomeXml, "fp-home"),
            ScreenStateResult(HomeXml, "fp-home"));
        var visual = new FakeVisualAnalyzer();

        var pipeline = NewPipeline(state, visual);

        var first = await pipeline.AnalyzeCurrentPageAsync();
        pipeline.MarkBackNavigation(); // no earlier distinct page in history

        var second = await pipeline.AnalyzeCurrentPageAsync();

        Assert.NotNull(second);
        Assert.Equal(2, state.RefreshCount);
    }

    // ── 7. 共享捕获缓存 ─────────────────────────────────────────────────

    [Fact(DisplayName = "D1: 共享 before-step 捕获有效时零额外 refresh")]
    public async Task CaptureStore_Valid_ZeroExtraRefresh()
    {
        var state = ScriptedScreenState.Succeed(HomeXml, "fp-home");
        var visual = new FakeVisualAnalyzer();
        var store = new FakeCaptureStore(
            new ScreenStateResult(
                true,
                "ok",
                HomeXml,
                "fp-home",
                false,
                false,
                null));

        var pipeline = NewPipeline(state, visual, captureStore: store);

        var analysis = await pipeline.AnalyzeCurrentPageAsync();

        Assert.NotNull(analysis);
        Assert.Equal(0, state.RefreshCount);
    }

    [Fact(DisplayName = "L2: AI 返回 null → pipeline 透传 null")]
    public async Task Ai_NullResult_PassesThrough()
    {
        var state = ScriptedScreenState.Succeed(SingleItemXml, "fp-sparse");
        var visual = new FakeVisualAnalyzer { Result = null };

        var pipeline = NewPipeline(state, visual);

        var analysis = await pipeline.AnalyzeCurrentPageAsync();

        Assert.Null(analysis);
        Assert.Equal(1, visual.Calls);
    }

    // ── Harness ─────────────────────────────────────────────────────────

    private static ScreenStateResult ScreenStateResult(
        string xml,
        string fingerprint) =>
        new(
            true,
            "scrollable",
            xml,
            fingerprint,
            true,
            false,
            null);

    private static ITraceRecorder NewTrace(out InMemoryTraceStorage storage)
    {
        storage = new InMemoryTraceStorage();
        return new InMemoryTraceRecorder(storage);
    }

    private static ObservationPipeline NewPipeline(
        ScriptedScreenState state,
        FakeVisualAnalyzer visual,
        ObservationConfig? config = null,
        IScreenStateCache? captureStore = null,
        ITraceRecorder? trace = null) =>
        new(visual, state, config, captureStore, trace);

    private sealed class FakeVisualAnalyzer : IPageAnalyzer
    {
        private static readonly PageAnalysis DefaultAiAnalysis = new(
            Direction.Left,
            Direction.Left,
            CurrentPath: ["AI Page"]);

        public int Calls { get; private set; }

        public Exception? Exception { get; set; }

        public PageAnalysis? Result { get; set; } = DefaultAiAnalysis;

        public Task<PageAnalysis?> AnalyzeCurrentPageAsync(
            CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Exception is not null)
                return Task.FromException<PageAnalysis?>(Exception);
            return Task.FromResult(Result);
        }

        public Task<AppEntryPoint?> FindAppEntryAsync(
            string targetApp,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AppEntryPoint?>(null);

        public Task<PageTypeVerification> VerifyPageTypeAsync(
            PageAnalysis pageAnalysis,
            string expectedType,
            string? expectedPageName = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PageTypeVerification(
                true,
                1,
                expectedType));
    }

    private sealed class ScriptedScreenState : IObservableScreenStateProvider,
        IUiAutomatorAvailability
    {
        private readonly Queue<ScreenStateResult> _results;

        public ScriptedScreenState(params ScreenStateResult[] results)
        {
            _results = new Queue<ScreenStateResult>(results);
        }

        public static ScriptedScreenState Succeed(string xml, string fingerprint) =>
            new(ScreenStateResult(xml, fingerprint));

        public int RefreshCount { get; private set; }

        public bool IsUiAutomatorAvailable { get; set; } = true;

        public bool HasScroll() => true;

        public double GetScrollProgress() => 0.5;

        public bool IsEndOfList() => false;

        public ScrollSwipeConfig? GetScrollSwipeConfig() => null;

        public Task<ScreenStateResult> RefreshAsync(
            string? previousHierarchyXml = null,
            bool afterScroll = false,
            CancellationToken cancellationToken = default)
        {
            RefreshCount++;
            if (_results.Count == 0)
                throw new InvalidOperationException("No more scripted results.");
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class FakeCaptureStore : IScreenStateCache
    {
        private readonly ScreenStateResult? _state;

        public FakeCaptureStore(ScreenStateResult? state)
        {
            _state = state;
        }

        public bool TryGetBefore(out ScreenStateResult? state)
        {
            state = _state;
            return true;
        }
    }
}
