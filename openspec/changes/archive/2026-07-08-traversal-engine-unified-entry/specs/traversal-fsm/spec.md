## MODIFIED Requirements

### Requirement: TraversalFSM defines exactly 8 states
No change to the 8 TraversalState values. This requirement is unchanged.

### Requirement: TraversalFSM transition matrix is enforced with D-1 correction
No change to the transition matrix. This requirement is unchanged.

### Requirement: TraversalFSM step dispatches by from_state to handler methods
**Change**: `HasUnvisitedChildren(IGraphTraversalEngine?)` parameter type changes from `UniClaw.Core.StateMachine.IGraphTraversalEngine` (empty stub) to `UniClaw.Core.Traversal.IGraphTraversalEngine` (full 8-member async interface). The empty stub in `TraversalState.cs` lines 152-155 SHALL be deleted. `TraversalFSM.cs` SHALL add `using UniClaw.Core.Traversal;` and reference the full interface for HasUnvisitedChildren parameter. `ITraversalStateMachine` interface (defined in TraversalState.cs) SHALL also update HasUnvisitedChildren parameter type.

#### Scenario: HasUnvisitedChildren receives TraversalEngine instance
- **WHEN** TraversalEngine implements IGraphTraversalEngine and passes itself to TraversalFSM
- **THEN** HasUnvisitedChildren can query the engine's visited children state (no longer always null/dead code)

#### Scenario: Empty stub deleted from TraversalState.cs
- **WHEN** the empty `public interface IGraphTraversalEngine {}` at TraversalState.cs:152-155 is removed
- **THEN** only `UniClaw.Core.Traversal.IGraphTraversalEngine` remains as the canonical interface definition

### Requirement: TraversalFSM and GlobalFSM are independent layers
No change to independence. TraversalEngine coordinates both FSMs through `ctx.GlobalState` field — this is the same coordination mechanism described in the original requirement (GlobalFSM writes, TraversalFSM reads as opaque context). The coordination does NOT create shared state between the FSMs — TraversalRuntimeContext.GlobalState is a data field, not FSM infrastructure.
