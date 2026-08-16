# Design: Semantic Run Popup Obstruction Integration

## Existing Mechanism (PlanRun)

PlanRun already supports local obstructions:

1. `CanHandleLocalObstruction(observation, page, app, step)` - checks if a plan step can handle a local obstruction
2. `IsLocalObstructionHypothesis(observation, page, app)` - detects same-foreground + unknown-page obstruction
3. `TryAcceptLocalObstruction(observation, page, app, step)` - accepts fresh obstruction observation without losing progress

## SemanticRun Gap

SemanticRun does not call these methods. When an unexpected popup appears, SemanticRun either:
- Fails with SemanticContradiction (page unknown)
- Tries to navigate to an unrelated page
- Or tries to execute a semantic action against the popup

## Integration Point

The integration point is at the START of each SemanticRun loop iteration, after the current observation has been obtained but before reading container bindings.

When `container.IsLocalObstructionHypothesis(observation, _belief?.SemanticPage, ready.Anchor.ApplicationIdentity)` is true:
1. Find a bounded handling action (dismiss/back) from the current observation
2. Execute it through Traversal
3. Obtain fresh Observation
4. Verify obstruction cleared (page/container reconciled)
5. Refresh Container evidence
6. Continue same Goal

## Implementation

### Helper method: `TryHandleLocalObstruction`

```csharp
private async Task<bool> TryHandleLocalObstructionAsync(
    SemanticGoalInput goal,
    RuntimeContainer container,
    Observation observation,
    WorldBelief belief,
    StartupResult.Ready ready,
    string runId,
    CancellationToken cancellationToken)
```

Returns true if handled (loop should continue), false if no obstruction or cannot handle.

### Insertion Point

In `RunSemanticGoalAsync`, at the top of the loop (after reading `container` and before checking `LocalPageBeliefState`):

```csharp
if (container.IsLocalObstructionHypothesis(observation, _belief?.SemanticPage, ready.Anchor.ApplicationIdentity))
{
    var handled = await TryHandleLocalObstructionAsync(...);
    if (handled) continue;
    // else fall through to existing failure path
}
```

## Test Matrix

POP-1 through POP-10 as specified in the proposal.
