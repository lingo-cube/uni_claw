using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.Observability;

/// <summary>
/// C-8/C-9/C-10: Trace collection completion 集成测试。
/// 覆盖 AICallRecord.Metadata、DurationMs、PageId、扩展向后兼容等。
/// </summary>
public class TraceCollectionCompletionTests
{
    // ── C-9: PopupHandlingResult 扩展向后兼容 ────────────────

    [Fact(DisplayName = "C-9: PopupHandlingResult backward compatible — Classification defaults null")]
    public void PopupHandlingResult_BackwardCompatible()
    {
        var result = new PopupHandlingResult(true, "dismiss", "Popup dismissed");
        Assert.True(result.Success);
        Assert.Equal("dismiss", result.Action);
        Assert.Equal("Popup dismissed", result.Description);
        Assert.Null(result.Classification);
    }

    [Fact(DisplayName = "C-9: PopupHandlingResult with Classification")]
    public void PopupHandlingResult_WithClassification()
    {
        var classification = new PopupClassification(
            PopupType.Dialog, "ok", DismissStrategy.AutoClose, UrgencyLevel.Medium, BlockingType.Modal);
        var result = new PopupHandlingResult(true, "dismiss", "Popup dismissed", classification);
        Assert.NotNull(result.Classification);
        Assert.Equal(PopupType.Dialog, result.Classification.PopupType);
    }

    // ── C-9: ContainerActionResult 扩展向后兼容 ──────────────

    [Fact(DisplayName = "C-9: ContainerActionResult backward compatible — optional fields default null")]
    public void ContainerActionResult_BackwardCompatible()
    {
        var result = new ContainerActionResult(FallbackAction.Back, true, "Done");
        Assert.Equal(FallbackAction.Back, result.Action);
        Assert.True(result.Success);
        Assert.Null(result.CompletionReason);
        Assert.Null(result.TotalChildren);
        Assert.Null(result.VisitedChildCount);
        Assert.Null(result.Depth);
    }

    [Fact(DisplayName = "C-9: ContainerActionResult with completion fields")]
    public void ContainerActionResult_WithCompletionFields()
    {
        var result = new ContainerActionResult(
            FallbackAction.AutoEscape, true, "Visited all",
            CompletionReason.AllVisited, 10, 10, 5);
        Assert.Equal(CompletionReason.AllVisited, result.CompletionReason);
        Assert.Equal(10, result.TotalChildren);
        Assert.Equal(10, result.VisitedChildCount);
        Assert.Equal(5, result.Depth);
    }

    // ── C-10: AICallRecord.Metadata ──────────────────────────

    [Fact(DisplayName = "C-10: AICallRecord backward compatible — Metadata defaults null")]
    public void AICallRecord_BackwardCompatible()
    {
        var record = new AICallRecord("vision", "provider", true, 150.0);
        Assert.True(record.Success);
        Assert.Equal(150.0, record.LatencyMs);
        Assert.Null(record.Metadata);
        Assert.Null(record.Tokens);
    }

    [Fact(DisplayName = "C-10: AICallRecord.Metadata round-trip")]
    public void AICallRecord_WithMetadata()
    {
        var metadata = new Dictionary<string, object> { ["adb_operation"] = "tap", ["adb_latency_ms"] = 150 };
        var record = new AICallRecord("vision", "provider", true, 150.0,
            Tokens: 100, Metadata: metadata);

        Assert.Equal(100, record.Tokens);
        Assert.NotNull(record.Metadata);
        Assert.Equal("tap", record.Metadata["adb_operation"]);
        Assert.Equal(150, record.Metadata["adb_latency_ms"]);
    }

    // ── C-8: TraceContext 4-field rule (PageId NOT on non-ExecutionRecord) ──

    [Fact(DisplayName = "C-8: TraceContext has 6 fields only")]
    public void TraceContext_Has6Fields()
    {
        var props = typeof(TraceContext).GetProperties();
        // TraceContext should only have: NodeId, StepSpanId, StepNumber, TraceId, VisitSpanId, ParentSpanId
        Assert.Equal(6, props.Length);
        Assert.Contains(props, p => p.Name == nameof(TraceContext.NodeId));
        Assert.Contains(props, p => p.Name == nameof(TraceContext.StepSpanId));
        Assert.Contains(props, p => p.Name == nameof(TraceContext.StepNumber));
        Assert.Contains(props, p => p.Name == nameof(TraceContext.TraceId));
        Assert.Contains(props, p => p.Name == nameof(TraceContext.VisitSpanId));
        Assert.Contains(props, p => p.Name == nameof(TraceContext.ParentSpanId));
    }

    [Fact(DisplayName = "C-8: ExecutionRecord has PageId, other record types don't")]
    public void PageId_OnlyOnExecutionRecord()
    {
        // ExecutionRecord has PageId
        var execProps = typeof(ExecutionRecord).GetProperties();
        Assert.Contains(execProps, p => p.Name == nameof(ExecutionRecord.PageId));

        // StateTransition does NOT have PageId
        var stProps = typeof(StateTransition).GetProperties();
        Assert.DoesNotContain(stProps, p => p.Name == "PageId");

        // ErrorRecord does NOT have PageId
        var errProps = typeof(ErrorRecord).GetProperties();
        Assert.DoesNotContain(errProps, p => p.Name == "PageId");

        // PageTransition has FromPage/ToPage (not PageId)
        var ptProps = typeof(PageTransition).GetProperties();
        Assert.DoesNotContain(ptProps, p => p.Name == "PageId");

        // AICallRecord does NOT have PageId
        var aiProps = typeof(AICallRecord).GetProperties();
        Assert.DoesNotContain(aiProps, p => p.Name == "PageId");
    }

    // ── C-8: GlobalFSM 状态覆盖 ─────────────────────────────

    [Fact(DisplayName = "C-8: GlobalState has 8 values, exclude 2 terminal = 6 active")]
    public void GlobalState_Has8Values_ExcludeTerminal()
    {
        var values = Enum.GetValues<GlobalState>();
        Assert.Equal(8, values.Length);

        var terminal = new[] { GlobalState.Completed, GlobalState.Terminated };
        var active = values.Where(v => !terminal.Contains(v)).ToArray();
        Assert.Equal(6, active.Length);
        Assert.Contains(GlobalState.Idle, active);
        Assert.Contains(GlobalState.Initializing, active);
        Assert.Contains(GlobalState.Traversing, active);
        Assert.Contains(GlobalState.Paused, active);
        Assert.Contains(GlobalState.Error, active);
        Assert.Contains(GlobalState.Recovering, active);
    }

    // ── C-9: DecideFrameCompletion rename ───────────────────

    [Fact(DisplayName = "C-9: InterceptionHandler has DecideFrameCompletionAsync (not sync version)")]
    public void DecideFrameCompletion_IsAsync()
    {
        var methods = typeof(InterceptionHandler).GetMethods(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var asyncMethod = methods.FirstOrDefault(m =>
            m.Name == "DecideFrameCompletionAsync" &&
            m.ReturnType == typeof(Task<(bool, bool, TraversalState)>));
        Assert.NotNull(asyncMethod);

        var syncMethod = methods.FirstOrDefault(m =>
            m.Name == "DecideFrameCompletion" &&
            m.ReturnType == typeof(ValueTuple<bool, bool, TraversalState>));
        Assert.Null(syncMethod);
    }
}
