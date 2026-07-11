using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.Observability;

/// <summary>
/// TraceCoordinator fill tests — SpanId generation, StepSpanId lifecycle,
/// BuildCorrelation produces TraceContext, typed RecordActionExecution,
/// RecordAICallSpan typed.
/// </summary>
public class TraceCoordinatorFillTests
{
    // ── Helper: create active coordinator with storage ────

    private (TraceCoordinator coord, InMemoryTraceStorage storage) CreateActiveCoordinator(
        ITraversalContext? ctx = null)
    {
        var storage = new InMemoryTraceStorage();
        var recorder = new InMemoryTraceRecorder(storage);
        var coord = new TraceCoordinator(recorder, "abc", ctx);
        return (coord, storage);
    }

    // ── SpanId generation ─────────────────────────────────

    [Fact(DisplayName = "TraceCoordinator: SpanId format is traceId-6digit counter")]
    public void SpanId_Format()
    {
        var (coord, storage) = CreateActiveCoordinator();
        coord.RecordStepStart("n1", "ok");
        coord.RecordStepEnd("n1", "ok");

        var executions = storage.GetExecutions();
        Assert.Equal(2, executions.Count);
        Assert.Equal("abc-000001", executions[0].SpanId);
        Assert.Equal("abc-000002", executions[1].SpanId);
    }

    // ── StepSpanId lifecycle ──────────────────────────────

    [Fact(DisplayName = "TraceCoordinator: StepSpanId = StepStart's SpanId")]
    public void StepSpanId_EqualsStepStartSpanId()
    {
        var (coord, storage) = CreateActiveCoordinator();
        coord.RecordStepStart("n1", "ok");
        // StepSpanId should be set to StepStart's SpanId

        var executions = storage.GetExecutions();
        var stepStart = executions[0];
        Assert.Equal("abc-000001", stepStart.SpanId);
        Assert.Equal("abc-000001", stepStart.Context?.StepSpanId);
    }

    [Fact(DisplayName = "TraceCoordinator: StepSpanId released at StepEnd")]
    public void StepSpanId_ReleasedAtStepEnd()
    {
        var (coord, storage) = CreateActiveCoordinator();
        coord.RecordStepStart("n1", "ok");
        coord.RecordStepEnd("n1", "ok");
        // After StepEnd, StepSpanId should be null for subsequent records

        coord.RecordStepStart("n2", "ok");
        var lastStepStart = storage.GetExecutions().Last();
        Assert.Equal("abc-000003", lastStepStart.SpanId);
        Assert.Equal("abc-000003", lastStepStart.Context?.StepSpanId);
    }

    // ── BuildCorrelation produces TraceContext ────────────

    [Fact(DisplayName = "TraceCoordinator: BuildCorrelation produces TraceContext from ctx")]
    public void BuildCorrelation_ProducesTraceContext()
    {
        // Create a mock context-like object for testing
        var ctx = new TraversalRuntimeContext("abc", maxDepth: 10);
        ctx.NodeStack.Push(new TraversalNode(
            NodeId: "wifi_node",
            Name: "WiFi Settings",
            NodeType: NodeType.Action,
            Operation: new Operation(OperationType.NoAction),
            ChildrenStrategy: new ChildrenStrategy(ChildrenStrategyType.None)));
        ctx.CurrentFrame = ctx.NodeStack.Peek()?.Node;
        ctx.IncrementStepCount(); // StepCount = 1

        var (coord, storage) = CreateActiveCoordinator(ctx);
        coord.RecordStepStart("wifi_node", "ok");

        var executions = storage.GetExecutions();
        Assert.Single(executions);
        Assert.Equal("wifi_node", executions[0].Context?.NodeId);
        Assert.Equal(1, executions[0].Context?.StepNumber);
        Assert.Equal("abc", executions[0].Context?.TraceId);
    }

    [Fact(DisplayName = "TraceCoordinator: null ctx produces TraceContext with TraceId + StepSpanId")]
    public void NullCtx_ProducesPartialTraceContext()
    {
        var (coord, storage) = CreateActiveCoordinator(ctx: null);
        coord.RecordStepStart("n1", "ok");

        var executions = storage.GetExecutions();
        Assert.Single(executions);
        // Partial correlation: TraceId + StepSpanId available, NodeId + StepNumber null
        Assert.NotNull(executions[0].Context);
        Assert.Null(executions[0].Context?.NodeId);
        Assert.Equal("abc-000001", executions[0].Context?.StepSpanId);
        Assert.Null(executions[0].Context?.StepNumber);
        Assert.Equal("abc", executions[0].Context?.TraceId);
    }

    // ── RecordRootNodePushed has null Context ──────────────

    [Fact(DisplayName = "TraceCoordinator: RecordRootNodePushed has null Context")]
    public void RecordRootNodePushed_NullContext()
    {
        var (coord, storage) = CreateActiveCoordinator();
        coord.RecordRootNodePushed("root");

        var transitions = storage.GetTransitions();
        Assert.Single(transitions);
        Assert.Null(transitions[0].Context);
        Assert.Equal("TraversalFSM", transitions[0].FsmType);
    }

    // ── Typed RecordActionExecution ────────────────────────

    [Fact(DisplayName = "TraceCoordinator: RecordActionExecution typed (OperationType, Target?, bool)")]
    public void RecordActionExecution_Typed()
    {
        var (coord, storage) = CreateActiveCoordinator();
        coord.RecordActionExecution(
            OperationType.Click,
            new Target(TargetType.Coordinate, new Coordinate(0.5, 0.3)),
            true);

        var executions = storage.GetExecutions();
        Assert.Single(executions);
        Assert.Equal("click", executions[0].Action);
        Assert.Equal(TargetType.Coordinate, executions[0].TargetType);
        Assert.Equal("0.5,0.3", executions[0].TargetValue);
    }

    [Fact(DisplayName = "TraceCoordinator: RecordActionExecution Back/NoAction → TargetType=null")]
    public void RecordActionExecution_BackNullTarget()
    {
        var (coord, storage) = CreateActiveCoordinator();
        coord.RecordActionExecution(OperationType.Back, null, true);

        var executions = storage.GetExecutions();
        Assert.Single(executions);
        Assert.Equal("back", executions[0].Action);
        Assert.Null(executions[0].TargetType);
        Assert.Null(executions[0].TargetValue);
    }

    // ── RecordAICallSpan typed ────────────────────────────

    [Fact(DisplayName = "TraceCoordinator: RecordAICallSpan typed with Context")]
    public void RecordAICallSpan_Typed()
    {
        var (coord, storage) = CreateActiveCoordinator();
        coord.RecordAICallSpan("vision", "provider", true, 230.5);

        var calls = storage.GetAICalls();
        Assert.Single(calls);
        Assert.Equal("vision", calls[0].Capability);
        Assert.Null(calls[0].Tokens);
    }

    [Fact(DisplayName = "TraceCoordinator: RecordAICallSpan with tokens")]
    public void RecordAICallSpan_WithTokens()
    {
        var (coord, storage) = CreateActiveCoordinator();
        coord.RecordAICallSpan("vision", "provider", true, 230.5, tokens: 1500);

        var calls = storage.GetAICalls();
        Assert.Single(calls);
        Assert.Equal(1500, calls[0].Tokens);
    }

    // ── RecordErrorSpan with Context ──────────────────────

    [Fact(DisplayName = "TraceCoordinator: RecordErrorSpan with Context")]
    public void RecordErrorSpan_WithContext()
    {
        var (coord, storage) = CreateActiveCoordinator();
        coord.RecordErrorSpan("popup", "unexpected popup", ErrorSeverity.Warning);

        var errors = storage.GetErrors();
        Assert.Single(errors);
        Assert.Equal("popup", errors[0].ErrorType);
        Assert.Null(errors[0].Context?.NodeId); // null ctx → null Context
    }

    // ── RecordPageTransition with Context ─────────────────

    [Fact(DisplayName = "TraceCoordinator: RecordPageTransition with Context")]
    public void RecordPageTransition_WithContext()
    {
        var (coord, storage) = CreateActiveCoordinator();
        coord.RecordPageTransition("home", "wifi", "forward");

        var pts = storage.GetPageTransitions();
        Assert.Single(pts);
        Assert.Equal("home", pts[0].FromPage);
        Assert.Equal("wifi", pts[0].ToPage);
    }

    // ── AllMethods_NoOpWhenInactive ────────────────────────

    [Fact(DisplayName = "TraceCoordinator: all methods no-op when inactive")]
    public void AllMethods_NoOpWhenInactive()
    {
        var coord = new TraceCoordinator(null, null);
        coord.RecordStateTransition("A", "B");
        coord.RecordRootNodePushed("node-1");
        coord.RecordPageAnalysis(null);
        coord.RecordActionExecution("click", "btn", true);
        coord.RecordActionExecution(OperationType.Back, null, true);
        coord.RecordErrorSpan("type", "msg", ErrorSeverity.Warning);
        coord.RecordPageTransition("/a", "/b", "nav");
        coord.RecordDynamicLifecycle("generate", "n1", "p1", "r1", "");
        coord.RecordStateDecision("continue", "n1", null);
        coord.RecordStepStart("n1", "");
        coord.RecordStepEnd("n1", "ok");
        coord.RecordAICallSpan("vision", "provider", true, 230.5);
        // No exceptions — all no-op when Active=False
    }

    // ── StepTraceSnapshot ──────────────────────────────────

    [Fact(DisplayName = "TraceCoordinator: GetStepSnapshot accumulates SpanTypes, resets on read")]
    public void GetStepSnapshot_AccumulatesAndResets()
    {
        var (coord, _) = CreateActiveCoordinator();
        coord.RecordStepStart("n1", "ok");      // StateDecision
        coord.RecordPageAnalysis(null);          // PageAnalysis

        var snapshot1 = coord.GetStepSnapshot();
        Assert.Contains(SpanType.PageAnalysis, snapshot1);
        Assert.Contains(SpanType.StateDecision, snapshot1);

        // Reset on read — second call returns empty
        var snapshot2 = coord.GetStepSnapshot();
        Assert.True(snapshot2.IsEmpty);
    }
}
