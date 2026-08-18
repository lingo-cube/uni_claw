## Context

SC-P3-003 proves one targetless `ScrollForward`, a fresh post-action Observation, and same-Container continuity. It intentionally does not retain evidence across multiple viewport movements, decide whether another movement is justified, or prove semantic exhaustion. `Container.CurrentObservation` retains only the latest snapshot, `ExecutedSteps` records mechanics rather than observed content, and Agent currently executes a fixed finite Plan without a three-way viewport-exploration decision surface.

SC-P3-CAND-007 requires bounded comparison of accepted Observations inside one semantic Container while preserving the existing ownership spine. The approved Gate permits one immutable two-field evidence value, one optional Goal criterion field, and one Container-owned retained-evidence field. It does not permit a production Viewport identity, manager, graph, stack, policy, or new mutable owner.

## Goals / Non-Goals

**Goals:**

- Retain a bounded sequence of accepted same-Container Observation evidence across repeated viewport movements.
- Represent positive continuation, positive exhaustion, and unresolved evidence distinctly with a deterministic reason.
- Authorize at most one additional movement per positive continuation decision.
- Require SC-P3-003 freshness and continuity before post-movement evidence enters retained exploration evidence.
- Stop honestly when exhaustion is positively proven, evidence is unresolved, or the approved movement bound is consumed.
- Keep exploration exhaustion separate from local completion, branch completion, and Run completion.
- Replay retained evidence, decisions, actions, journal, Trace, GoalEvidence, and final state deterministically.

**Non-Goals:**

- Add `Viewport`, `ViewportId`, stable content identity, viewport hierarchy, graph, stack, or manager.
- Treat sequence number, element index, text equality, snapshot equality, dispatch rejection, or budget exhaustion as semantic exhaustion.
- Add Fingerprint authority, scrolling policy, retry/uncertainty framework, dynamic planner, Recovery behavior, or multi-Container exploration state.
- Implement Capstone, S1/S2/S3, Vision/device behavior, Harness changes, or Runtime refactoring.

## Decisions

### Add one immutable three-valued exploration evidence value

Add `ViewportExplorationEvidence` with exactly two immutable fields:

```csharp
bool? ContinueExploration
string Reason
```

`true` means the supplied bounded same-Container evidence positively justifies one additional forward movement. `false` means it positively proves forward semantic exploration exhaustion. `null` means neither conclusion is proven. `Reason` must be non-empty and deterministic.

The value is evidence consumed by Agent. It is not a viewport object, progress counter, execution result, completion flag, or persistent decision state.

Alternative rejected: one boolean. It cannot distinguish positive exhaustion from insufficient evidence.

Alternative rejected: reuse `GoalEvidence`. GoalEvidence has final Goal-completion meaning and cannot become a local exploration outcome.

Alternative rejected: reuse `TraversalStepResult`. Mechanical dispatch/verification does not decide semantic continuation or exhaustion.

### Carry one optional bounded exploration criterion on Goal

Add one optional immutable Goal field with semantic shape:

```csharp
Func<ImmutableArray<Observation>, ViewportExplorationEvidence>?
    ViewportExplorationEvaluator
```

The input is an immutable, bounded sequence of Observations accepted within the same Container; the final item is the current fresh evidence. The evaluator must be deterministic, side-effect-free, and may use only its supplied evidence plus immutable Goal scope captured by the caller. It cannot call Environment, dispatch actions, mutate Runtime owners, or set RunState.

Agent is the only consumer and the sole authority that converts the result into continue, stop, or escalation behavior. When the evaluator is absent, existing fixed-Plan behavior remains unchanged and Container need not perform repeated-exploration decisions.

Alternative rejected: place the criterion in Container. Container owns local evidence but does not own Goal relevance or the high-level continue/stop decision.

Alternative rejected: place it in Traversal. Traversal owns one movement's mechanics and cannot interpret semantic exploration scope.

### Retain accepted evidence in one Container-owned field

Container gains one bounded retained-evidence field exposed only as an immutable snapshot. `Bind` starts a new Container-local sequence from the bound Observation. Every SC-P3-003 viewport continuity success appends the fresh Observation; rejected, stale, identity-conflicting, or unresolved continuity evidence is not appended.

The sequence is bounded by the existing finite approved `ScrollForward` steps in Plan plus the initial Observation. No new limit field, ScrollPolicy, counter owner, or history component is introduced. Observation sequence proves freshness/order only. Element text, state, and index remain evidence and are never promoted to stable content identity.

Alternative rejected: reconstruct decision state from Trace or Traversal journal. Those surfaces are receipts and mechanics, not Container-owned semantic-page evidence.

Alternative rejected: store retained evidence in both Agent and Container. That would duplicate mutable ownership.

### Agent authorizes one movement per positive decision

Before the first viewport movement and after every accepted post-movement Observation, Agent evaluates the optional criterion over the Container snapshot and records the outcome/reason through existing Trace evidence.

- `true` authorizes at most the next already-approved `ScrollForward` Plan step.
- `false` prohibits further viewport movement for this bounded exploration.
- `null` prohibits further movement and produces explicit unresolved/non-completion behavior.

The finite number of approved viewport Plan steps is the maximum exploration bound. If the latest decision remains `true` after the final approved movement is consumed, Agent reports bound exhaustion as unresolved/incomplete. It does not reinterpret it as semantic exhaustion or add a generic scrolling loop.

### Require positive exhaustion evidence

`ContinueExploration=false` is valid only when the supplied fresh, continuous, bounded evidence positively supports forward exhaustion. An explicit external end/boundary indication may satisfy the injected criterion. A separately evidenced multi-sample protocol may also do so, but equality alone cannot.

The following inputs cannot independently produce positive exhaustion: one unchanged element set, rejected/timed-out dispatch, consumed movement budget, no currently authorized candidate, unchanged Fingerprint, or absence of new text. Conflicting or insufficient evidence must return `null`.

### Keep exploration outcome separate from completion

Positive exhaustion stops further viewport requests only. It does not set `Container.IsLocalComplete`, branch completion, GoalEvidence, or RunState. Agent may set `RunState.Completed` only after consuming independently satisfied GoalEvidence. An unresolved exploration outcome cannot be silently converted into completion.

## Risks / Trade-offs

- [Risk] Retaining full Observation values can increase memory use. → Mitigation: retain only the bounded same-Container sequence covered by the finite approved movement steps; no unbounded history is purchased.
- [Risk] A caller-provided evaluator could treat text equality as truth. → Mitigation: normative requirements require positive evidence and force ambiguous/conflicting inputs to `null`; formal negative tests cover equality, rejection, and bound shortcuts.
- [Risk] A nullable boolean can be read as a generic policy result. → Mitigation: its meaning is restricted to this bounded one-Container exploration evidence and requires a reason.
- [Risk] Agent receives another control-flow branch. → Mitigation: keep it optional and bounded, preserve existing owners, and record structural pressure without refactoring.
- [Risk] Exhaustion may be mistaken for Goal completion. → Mitigation: formal tests require separate satisfied GoalEvidence and prove exhaustion alone cannot complete the Run.
