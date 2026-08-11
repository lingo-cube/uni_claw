# Runtime Internal Consolidation Implementation

> Date: 2026-08-11
> Authorization: `RUNTIME_INTERNAL_CONSOLIDATION_IMPLEMENTATION`
> Lane: `CAPABILITY_DELIVERY_FAST`
> Result: `VALIDATED`
> OpenSpec: no matching active change exists; this is an explicitly authorized,
> behavior-preserving internal consolidation, not a capability delivery

## 1. Authorized scope

This implementation makes the existing Runtime easier to read and maintain
inside its frozen owners. It does not add a project facade, provider framework,
semantic capability, state owner, decision authority, or dependency edge.

Preserved truth:

- Agent remains Agent and owns the complete run-level semantic lifecycle.
- Container remains the sole page-local mutable belief/state owner.
- Traversal retains grounding, lowering, local execution, and verification.
- Environment retains physical dispatch.
- Stateless evidence producers own no mutable Runtime state and do not declare
  world truth.
- The dependency direction remains
  `Agent -> Container -> Traversal -> Environment`.

Explicitly outside scope:

- `Vision`, `Brain`, or `Operator` facade implementation;
- provider interfaces, registries, or a production composition root;
- `StateClassifier`;
- new business semantics or public authority surfaces;
- ownership, safety, recovery, completion, or world-truth changes.

## 2. Consolidated internal structure

### 2.1 Agent remains one type and one authority

The large Agent implementation is split across partial files by existing run
responsibility:

| File | Responsibility |
|---|---|
| `Agent/Agent.cs` | dependencies, Run-owned state, public state surface, shared helpers |
| `Agent/Agent.PlanRun.cs` | deterministic Plan Run loop |
| `Agent/Agent.OpenWorld.cs` | bounded open-world execution |
| `Agent/Agent.Recovery.cs` | Agent-owned recovery decisions and resume flow |
| `Agent/Agent.SemanticRun.cs` | structured semantic-goal closed loop |

All files compile into the same sealed `Agent` type. This is a source layout
boundary only: it creates no second lifecycle, state owner, or public component.

### 2.2 Action authorization remains Agent authority

`ActionAuthorizer` is an `internal` stateless validation helper. The public
authority surface remains `Agent.AuthorizeAction`; callers cannot obtain a new
authorization service or bypass Agent. The helper owns no mutable state and
does not select a capability or target.

### 2.3 Binding evidence naming is responsibility-specific

`World/BindingAnalysis.cs` names the existing observation-scoped binding
evidence responsibility more precisely than the earlier `ElementAnalysis`
working name:

```text
Observation + ElementBindingCriteria
  -> immutable SemanticEvidence
  -> immutable ObjectBinding candidates
  -> Container accepts the page-local snapshot
```

`BindingAnalysis` is stateless and depends only on Model types. It neither owns
the resulting Container state nor declares object identity as world truth.

### 2.4 Same-owner duplication is removed locally

Container and Traversal keep consolidation helpers private/internal to their
existing owner. Shared step-result recording, continuity Trap construction, and
journal append mechanics do not become services or new authority boundaries.

## 3. Architecture fit

| Check | Result |
|---|---|
| Mutable-state ownership unchanged | **PASS** |
| Semantic decision authority unchanged | **PASS** |
| Dependency direction unchanged | **PASS** |
| Safety/action authorization unchanged | **PASS** |
| External-world authority unchanged | **PASS** |
| Public facade/provider surface added | **NO** |
| New semantic capability added | **NO** |
| `StateClassifier` implemented | **NO** |

Result: `ARCHITECTURE_FIT_CONFIRMED`.

## 4. Implementation and verification

**Implementation:** Existing behavior is organized into cohesive same-owner
partial files and internal/private helpers. The Runtime construction model and
public owner boundaries remain unchanged.

**Invariant Verification:** `scripts/check-consistency.sh` passes C1-C10,
including all 14 invariants, zero ProjectReference, zero legacy Runtime
namespace references, and the shared routing checks.

**Test Verification:**

- `dotnet build src/UniClaw.Runtime.sln --no-restore`: 0 warnings, 0 errors.
- `dotnet test src/UniClaw.Runtime.sln --no-restore`: 661/661 passed.

## 5. Final state

```text
RUNTIME_INTERNAL_CONSOLIDATION_IMPLEMENTATION
  = VALIDATED

RUNTIME_PUBLIC_ARCHITECTURE
  = UNCHANGED

FACADE_OR_PROVIDER_IMPLEMENTATION
  = NOT_INTRODUCED

STATE_CLASSIFIER
  = DEFERRED_NOT_IMPLEMENTED
```
