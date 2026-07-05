# Design: Phase 2.3a — Core Traversal Loop

## Key Architecture Decision: Step(StepContext) Overload

Current TraversalFSM has `Step()` with no parameters — handlers only access `ITraversalContext` via `Context` field. For real handler logic, they need `IActionExecutor` and `IVisionProvider`.

### Option A: Constructor injection
```csharp
public TraversalFSM(ITraversalContext context, IActionExecutor action, IVisionProvider vision)
```
- Problem: FSM is state machine — action/vision change per step (different targets, different pages)
- FSM shouldn't own services; services are per-step, not per-FSM-lifetime

### Option B: Step(StepContext ctx) overload
```csharp
public TraversalState Step(StepContext ctx)
{
    // ctx.Vision, ctx.Action available to handlers
    // Existing Step() calls Step(null) for stub compatibility
}
```
- Matches Python pattern: `_handle_execute(self, stack, context, vision, action)`
- StepContext already exists with 13 fields including Vision + Action
- Non-breaking: existing `Step()` delegates to `Step(null)` with default behavior
- **Selected**: Option B (→ D-18)

### Handler access pattern
```csharp
private TraversalState HandleExecute()
{
    if (_currentStepContext == null)
        return TraversalState.ResultVerify; // Stub fallback

    var node = Context.NodeStack.Peek()?.Node;
    if (node?.Operation == null)
        return TraversalState.ResultVerify;

    try
    {
        var result = ExecuteOperation(node.Operation, _currentStepContext.Action);
        // ... restore logic, metrics ...
        return TraversalState.ResultVerify;
    }
    catch (Exception ex)
    {
        Context.LastError = ex;
        return TraversalState.ErrorHandling;
    }
}
```

## HandleExecute Design

### Decision flow
```
HandleExecute()
  │
  ├─ No StepContext → ResultVerify (stub fallback)
  ├─ No current node → ResultVerify (edge case)
  ├─ No operation → ResultVerify (leaf/container nodes)
  │
  ├─ Execute primary operation (IActionExecutor)
  │   ├─ Success → check needs_restore
  │   │   ├─ Has RestoreAction → execute restore
  │   │   │   ├─ Success → ResultVerify
  │   │   │   ├─ Failure → ResultVerify (restore failure is not critical)
  │   │   └─ No restore → ResultVerify
  │   ├─ Failure → set LastError → ErrorHandling
  │
  ├─ Exception → set LastError → ErrorHandling
```

### Operation execution mapping
| OperationType | IActionExecutor method |
|--------------|----------------------|
| Click | TapAsync(target.X, target.Y) |
| Swipe | SwipeAsync(start.X, start.Y, end.X, end.Y, duration) |
| Back | PressBackAsync() |
| Input | InputTextAsync(text) |
| LongPress | LongPressAsync(target.X, target.Y, duration) |
| Wait | WaitAsync(duration) |

## HandleBranch Design

### Decision flow
```
HandleBranch()
  │
  ├─ No current node → NodeSelect or FrameComplete
  │
  ├─ Check ChildrenStrategy type
  │   ├─ NONE → leaf or container
  │   │   ├─ IsLeaf → FRAME_COMPLETE (or NODE_SELECT if root)
  │   │   ├─ Not leaf, no unvisited → FRAME_COMPLETE
  │   ├─ STATIC → check static children against visited
  │   │   ├─ Has unvisited → NODE_SELECT
  │   │   ├─ All visited → FRAME_COMPLETE
  │   ├─ DYNAMIC_MATCH → optimistic NODE_SELECT
  │     (StepOrchestrator gates actual availability at engine level)
  │
  └─ Result: NodeSelect / FrameComplete
```

### ChildrenStrategy resolution
```csharp
private bool HasUnvisitedChildren(ITraversalNode node)
{
    if (node.ChildrenStrategy.Type == ChildrenStrategyType.None)
        return false;

    if (node.ChildrenStrategy.Type == ChildrenStrategyType.Static)
    {
        var visited = Context.VisitedChildren.GetValueOrDefault(node.NodeId, ImmutableHashSet<string>.Empty);
        return node.StaticChildren.Any(childId => !visited.Contains(childId));
    }

    // DYNAMIC_MATCH: optimistic — engine layer gates actual availability
    return true;
}
```

## Mock Infrastructure (Test-only)

### MockActionExecutor
```csharp
public sealed class MockActionExecutor : IActionExecutor
{
    public bool NextResult { get; set; } = true;
    public List<ActionRecord> CallLog { get; } = new();

    public Task<bool> TapAsync(double x, double y, CancellationToken ct = default)
    { CallLog.Add(new("tap", DateTimeOffset.UtcNow, new() { ["x"] = x, ["y"] = y }, NextResult)); return Task.FromResult(NextResult); }
    // ... similar for Swipe, PressBack, InputText, LongPress, Wait
}
```

### MockVisionProvider
```csharp
public sealed class MockVisionProvider : IVisionProvider
{
    public PageAnalysis? NextResult { get; set; }
    public Task<PageAnalysis?> GetCurrentPageAnalysisAsync(CancellationToken ct = default)
        => Task.FromResult(NextResult);
}
```
