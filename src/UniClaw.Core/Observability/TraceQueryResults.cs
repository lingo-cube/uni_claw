using System.Collections.Immutable;

namespace UniClaw.Core.Observability;

/// <summary>
/// 6 query result types — all sealed record classes with ImmutableArray fields.
/// Computed at query time from flat records + indexes, NOT stored in ITraceStorage.
/// </summary>

/// <summary>
/// TraversalTree — DFS tree reconstruction from DfsForward edges.
/// </summary>
/// <param name="Edges">Tree edges (Parent→Child) from DfsForward ExecutionRecords</param>
/// <param name="RootNodeId">Root node ID (first parent with no incoming child edge)</param>
public sealed record class TraversalTree(
    ImmutableArray<TreeEdge> Edges,
    string RootNodeId);

/// <summary>
/// TreeEdge — single DFS parent→child edge.
/// </summary>
/// <param name="Parent">Parent node ID (from DfsForward record's Context?.NodeId)</param>
/// <param name="Child">Child node ID (from DfsForward record's ChildNodeId)</param>
/// <param name="Depth">Traversal depth at time of edge</param>
/// <param name="EntryStep">Step number when child was pushed</param>
public sealed record class TreeEdge(
    string? Parent,
    string Child,
    int? Depth = null,
    int? EntryStep = null);

/// <summary>
/// NodeSpans — all 5 record types aggregated by NodeId via Context?.NodeId.
/// </summary>
/// <param name="NodeId">The node identifier</param>
/// <param name="Executions">ExecutionRecords at this node</param>
/// <param name="Errors">ErrorRecords at this node</param>
/// <param name="PageTransitions">PageTransitions at this node</param>
/// <param name="Transitions">StateTransitions at this node</param>
/// <param name="AICalls">AICallRecords at this node</param>
public sealed record class NodeSpans(
    string NodeId,
    ImmutableArray<ExecutionRecord> Executions,
    ImmutableArray<ErrorRecord> Errors,
    ImmutableArray<PageTransition> PageTransitions,
    ImmutableArray<StateTransition> Transitions,
    ImmutableArray<AICallRecord> AICalls);

/// <summary>
/// NodeVisitTimeline — entry and exit steps for a node visit.
/// </summary>
/// <param name="NodeId">The node identifier</param>
/// <param name="EntryStep">Step number when node was entered (DfsForward)</param>
/// <param name="ExitStep">Step number when node was exited (DfsBacktrack)</param>
public sealed record class NodeVisitTimeline(
    string NodeId,
    int? EntryStep = null,
    int? ExitStep = null);

/// <summary>
/// StepTimeline — all 5 record types aggregated by StepNumber via Context?.StepNumber.
/// </summary>
/// <param name="StepNumber">The step number</param>
/// <param name="Executions">ExecutionRecords at this step</param>
/// <param name="Transitions">StateTransitions at this step</param>
/// <param name="Errors">ErrorRecords at this step</param>
/// <param name="PageTransitions">PageTransitions at this step</param>
/// <param name="AICalls">AICallRecords at this step</param>
public sealed record class StepTimeline(
    int StepNumber,
    ImmutableArray<ExecutionRecord> Executions,
    ImmutableArray<StateTransition> Transitions,
    ImmutableArray<ErrorRecord> Errors,
    ImmutableArray<PageTransition> PageTransitions,
    ImmutableArray<AICallRecord> AICalls);

/// <summary>
/// StepSpanGroup — all 5 record types aggregated by StepSpanId via Context?.StepSpanId.
/// </summary>
/// <param name="StepSpanId">The per-engine-step grouping key</param>
/// <param name="Executions">ExecutionRecords in this span</param>
/// <param name="Transitions">StateTransitions in this span</param>
/// <param name="Errors">ErrorRecords in this span</param>
/// <param name="PageTransitions">PageTransitions in this span</param>
/// <param name="AICalls">AICallRecords in this span</param>
public sealed record class StepSpanGroup(
    string StepSpanId,
    ImmutableArray<ExecutionRecord> Executions,
    ImmutableArray<StateTransition> Transitions,
    ImmutableArray<ErrorRecord> Errors,
    ImmutableArray<PageTransition> PageTransitions,
    ImmutableArray<AICallRecord> AICalls);
