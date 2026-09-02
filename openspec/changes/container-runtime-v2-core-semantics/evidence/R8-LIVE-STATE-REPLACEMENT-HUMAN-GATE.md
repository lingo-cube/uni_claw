# R8 Live State Replacement Human Gate

STATUS: `CONTAINER_RUNTIME_V2_LIVE_STATE_REPLACEMENT_APPROVED_BOUNDED`

CURRENT_VALIDATED_STATE: `CONTAINER_RUNTIME_V2_AGENT_INTEGRATION_VALIDATED`

## Gate trigger

The bounded R7 composition facade and Agent correction consumer are validated, but repository search finds zero production callers of `ContainerRuntimeV2.Start`, `CompleteSlow`, or `ComposeAsync`. Fresh Phase 2.6 production acceptance cannot test Runtime V2 while the live Agent continues to commit only the previous `WorldBelief` / `ActiveContainerContext` / legacy typed-transition path.

The current approved decision explicitly says it does **not** authorize migration of the old live current/execution path. Wiring V2 now would therefore cross an explicit authority boundary, even if the implementation were mechanically small.

## Existing live ownership

| Live value | Current owner | Current meaning | Reconciliation |
|---|---|---|---|
| `Agent._belief` | Agent | latest accepted semantic interpretation of observed location | MOVE to a derived compatibility projection if V2 becomes live |
| `Agent._activeContainerContext` | Agent | active execution/completeness obligation plus verified ancestor path | KEEP as execution obligation only; never physical-current truth |
| `Container.CurrentObservation` | Container | current page-local accepted observation | KEEP as Slice/local-evidence source |
| `Agent._trace` typed transition entries | Agent | append-only compatibility/audit evidence | KEEP history; MOVE current occurrence authority to V2 commit |
| `Agent._branchProgress` | Agent | obligation/progress evidence | KEEP unchanged; correction continues through the sole Agent consumer |
| `ContainerRuntimeV2State` | no live owner | immutable V2 Graph/current/occurrence state used only by tests/composition calls | requires an explicitly approved live owner or a Harness-only shadow owner |

## First divergence

```text
Authorized Action
→ Fresh Observation
→ old TryPrepareContainerReconciliation
→ old CommitContainerReconciliation
→ _belief / _activeContainerContext / _trace
→ no ContainerRuntimeV2 lifecycle call
```

The first divergence is therefore the Agent reconciliation preparation/commit boundary, not Graph, Fast, Slow, or the correction consumer.

## Rejected implicit option

`PARALLEL_LIVE_V2_STATE` is rejected. Adding `_containerRuntimeV2State` while leaving `_belief` as an independent mutable current-location owner would create a second live current interpretation and would not prove `NET_NEW_MUTABLE_TRUTH = 0`. Storing mutable latest Fast, Slow, trust, correction, or checkpoint values is also rejected.

## Decision options

### Option A — Approve bounded live state replacement (recommended)

Authorize one staged, reversible Agent-module migration at the existing atomic reconciliation seam:

1. `ContainerRuntimeV2State` becomes the sole Agent-owned physical-current / Graph-occurrence aggregate.
2. `_belief` ceases to be an independent mutable current-location slot and becomes a derived compatibility projection from the accepted V2 current node/Slice semantic evidence.
3. `ActiveContainerContext` remains the execution/completeness obligation path only; `Observed != Execution` remains explicit.
4. Legacy typed transition records remain append-only compatibility evidence, produced from the same accepted occurrence; they cannot be current truth.
5. `_branchProgress`, GoalEvidence, action authorization, recovery, Container local observations, and Driver authority remain unchanged.
6. No mutable latest assessment/trust/correction/checkpoint slot is added.
7. Initial Apply runs Slow `Disabled`; Shadow/AsyncAdvisory remains optional until a provider experiment is separately available.

Required proof before commit:

```text
current physical-location owners: Agent 1 → Agent 1
semantic-current mutable slots: _belief 1 → V2 current + derived compatibility view 1
execution-obligation owners: ActiveContainerContext 1 → 1
progress owners: _branchProgress 1 → 1
mutable trust/checkpoint/correction slots: 0 → 0
NET_NEW_MUTABLE_TRUTH = 0
```

This option enables production Fast-only Phase 2.6 acceptance and later optional Slow Shadow without changing action or Goal authority.

### Option B — Approve Harness-only shadow acceptance

Keep the live Agent path unchanged and let ValidationHarness replay accepted action/observation evidence through the stateless V2 facade into a Harness-owned experiment state. This is reversible and authority-free, but it validates only shadow composition quality. It cannot establish that Runtime V2 is the production current-world path and cannot support graduation.

### Option C — Keep R7 contract-only state

Do not wire or shadow V2. The current bounded contracts remain validated, but Phase 2.6 Runtime V2 acceptance stays blocked.

## Sol recommendation

Approve **Option A** as a staged live replacement. It is the only option that tests the purchased CurrentContainer/Graph/Fast semantics in the real Runtime path while preserving `Observed != Execution`, Agent obligation authority, and `NET_NEW_MUTABLE_TRUTH = 0`. Option B is useful only if live replacement risk must first be reduced with shadow evidence.

## Separate environment blocker

No eligible ADB device is currently online. An attempt to start the existing `p26_pixel` emulator was rejected by the Codex host because the account usage limit prevented the required elevated launch. This is an environment/usage blocker, not Runtime evidence and not an architecture decision.

HUMAN_DECISION:

```text
Approved Option A: CONTAINER_RUNTIME_V2_LIVE_STATE_REPLACEMENT_APPROVED_BOUNDED
```
