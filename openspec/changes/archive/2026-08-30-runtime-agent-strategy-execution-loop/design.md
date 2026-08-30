## Context

See `proposal.md` for motivation and `specs/runtime-agent-strategy-execution-loop/spec.md` for normative behavior.

Two prerequisite capabilities already define the outer boundaries:

1. `uniagent-runtimeagent-strategy-contract` admits one immutable UniAgent-authored `StrategyDirective` and produces one bounded `RuntimeExecutionIntent` for at most one Agent-owned Run.
2. `runtime-agent-pre-terminal-cycle-contract` lets Agent create an immutable checkpoint, receive one passive `PreTerminalContinuationProposal`, validate it, and authorize transactional reasoning revision commit without transferring continuation or lifecycle authority.

The current missing connection is run-scoped RuntimeAgent reasoning that consumes accepted evidence during the Run. The design must preserve UniAgent ownership of Strategy and supervisory planning; RuntimeAgent ownership of local hypotheses, reconciliation, adaptation, and reasoning history; Agent ownership of RunState, checkpoint acceptance, action authorization, recovery, GoalEvidence, and terminal outcome; FSM ownership of lifecycle transition protocol; Traversal ownership of concrete execution; and Environment ownership of Observation acquisition.

## Goals / Non-Goals

**Goals:**

- Bind one accepted runtime execution intent to one RuntimeAgent-owned reasoning session for one Agent-owned Run.
- Provide a typed, immutable, scenario-neutral projection of accepted evidence sufficient for hypothesis reconciliation.
- Reuse existing Phase 2-4 reasoning records internally and the existing passive pre-terminal proposal externally.
- Commit reasoning history only after Agent accepts a fresh correlated proposal.
- Reuse the existing generic discovery and traversal engine without introducing another execution loop.
- Separate hypothesis stability from Agent-owned completion.

**Non-Goals:**

- No user-intent interpretation, Strategy generation, Strategy replacement, or user-level Plan inside RuntimeAgent.
- No action, target, route, selector, DFS ordering, recovery command, lifecycle command, completion assertion, or execution callback in reasoning contracts.
- No new traversal algorithm, DFS rewrite, Traversal rewrite, recovery redesign, or Environment redesign.
- No semantic capability protocol, external knowledge package, scenario migration, or external semantic dependency.
- No new Run, successor Run, outer loop, or Multi-Run orchestration.
- No external wire operation or Strategy payload change.

## Decisions

### 1. Architecture fit is additive and moves no authority

This change is an internal contract integration under the frozen top-level and Runtime authority models. The session participates in checkpoints selected by Agent; it does not own checkpoint timing or execution progress.

| Owner | Responsibility after this change |
|---|---|
| UniAgent | user intent, supervisory planning, Strategy creation, Strategy selection |
| RuntimeAgent reasoning | accepted intent interpretation, current ExecutionHypothesis, internal RuntimeDecision, internal HypothesisAdaptation, accepted reasoning history |
| Agent | Run identity and RunState, checkpoint timing and validation, proposal acceptance, same-Run continuation, candidate/action authorization, recovery, verification, GoalEvidence, terminal outcome |
| FSM | lifecycle transition protocol |
| Traversal | concrete execution of Agent-authorized work |
| Environment | Observation acquisition |
| Semantic capability | optional evidence enrichment before Agent accepts evidence; no dependency from this loop |

Alternative considered: let the reasoning session choose continuation directly. Rejected because a passive proposal would become a lifecycle command and duplicate Agent authority.

### 2. Reuse Strategy admission; do not add another Strategy interpreter

The existing Strategy compiler remains the only admission and normalization boundary. It validates the immutable Strategy and produces `RuntimeExecutionIntent`. The new session consumes only the accepted intent; it never parses user language or regenerates Strategy.

The relationship is frozen as:

| Artifact | Owner | Meaning |
|---|---|---|
| `StrategyDirective` | UniAgent | immutable bounded execution policy, constraints, evidence expectations, adaptation boundary |
| `RuntimeExecutionIntent` | RuntimeAgent admission | normalized one-Run boundary and existing Agent-consumable generic execution specification |
| `ExecutionHypothesis` | RuntimeAgent reasoning session | current revisable assumption about progress and expected evidence |
| `RuntimeDecision` | RuntimeAgent internal | reconciliation classification for one checkpoint |
| `HypothesisAdaptation` | RuntimeAgent internal | tentative permitted hypothesis revision |
| `PreTerminalContinuationProposal` | RuntimeAgent to Agent | sole passive Agent-facing projection of reasoning support |

Alternative considered: map `StrategyDirective` directly to `ExecutionHypothesis`. Rejected because it bypasses admission, capability validation, immutable normalized boundaries, and the one-intent-to-one-Run correlation.

Alternative considered: add a generative `StrategyInterpreter`. Rejected because it duplicates admission and creates pressure for RuntimeAgent to become a user-level planner.

### 3. Create one StrategyExecutionReasoningSession at the Run admission boundary

After Strategy admission returns one `RuntimeExecutionIntent` and Agent assigns the Run identity, composition creates one `StrategyExecutionReasoningSession` before the first eligible checkpoint. Agent remains the Run identity creator; session creation cannot start or transition the Run.

The session is bound immutably to:

- `RunId`
- `RuntimeExecutionIntentReference`
- the accepted Strategy adaptation boundary
- reasoning mode `PreTerminalStrategy`
- initial `ExecutionHypothesis H0`
- accepted reasoning revision `N0`

`H0` is a provisional, intent-bounded hypothesis. It may describe expected structural progress categories but carries no concrete world claim, route, target, action, or branch ordering. The first accepted checkpoint evidence grounds or revises it.

The session owns only reasoning bookkeeping:

```text
active accepted revision N
    + immutable ExecutionHypothesis
    + accepted internal decision/adaptation history
    + accepted evidence correlation

evaluation workspace
    + tentative RuntimeDecision
    + optional tentative HypothesisAdaptation
    + proposed revision N+1
```

Tentative records never enter accepted history before Agent compare-and-accept.

The session has two internal availability states, `Active` and `Sealed`. These are not RunState and cause no FSM transition. Agent-owned terminal finalization seals the session. A sealed session rejects all later evaluation and commit requests and may expose only a read-only reasoning receipt.

Alternative considered: create a long-lived RuntimeAgent coordinator shared by Runs. Rejected because it weakens correlation, encourages cross-Run state, and risks Multi-Run orchestration.

### 4. Embed StrategyExecutionEvidenceView in the existing immutable checkpoint

The pre-terminal contract already allows a bounded immutable evidence projection. For `PreTerminalStrategy` mode, Agent attaches exactly one `StrategyExecutionEvidenceView` to the existing `PreTerminalReasoningSnapshot`. The existing evaluator direction remains Agent to RuntimeAgent; no reverse callback or side-channel resolver is introduced.

Minimum shape:

```text
StrategyExecutionEvidenceView
  ContractVersion
  RunId
  RuntimeExecutionIntentReference
  AcceptedObservationSequence
  BeliefRevision
  BeliefDigest
  StructuralProgressRevision
  StructuralProgressFacts[]
  CoverageEvidenceReferences[]
  ContradictionEvidenceReferences[]
  TraceReferences[]
  TraceDigest
  EvidenceViewDigest
```

`StructuralProgressFacts` use a closed typed vocabulary such as:

- bounded scope entered
- child obligation discovered
- coverage obligation recorded
- coverage obligation resolved
- continuity verified
- contradiction observed

Each fact contains a kind, revision, and opaque evidence reference. It contains no actionable entity, location, branch priority, or arbitrary scenario text. A coverage reference proves only that a coverage record exists; it is not a completion flag.

The evidence view must repeat critical snapshot correlations. Agent validates equality between the snapshot and view before dispatch and again before proposal acceptance. `EvidenceViewDigest` binds the complete projection to the proposal evaluation even when the underlying World implementation evolves.

RuntimeAgent needs this view instead of direct World internals for four reasons:

1. **Ownership:** direct access would let reasoning observe mutable Agent-owned state outside the checkpoint transaction.
2. **Freshness:** an immutable digestible projection makes stale asynchronous results rejectable.
3. **Coupling:** the World and DFS representations may evolve without changing the reasoning contract.
4. **Neutrality:** a closed typed vocabulary prevents internal object graphs or scenario interpretation from leaking into RuntimeAgent reasoning.

Alternative considered: give the session a WorldBelief or DFS reference. Rejected because mutation, timing, coupling, and authority boundaries become unverifiable.

Alternative considered: let the session fetch evidence through a callback. Rejected because it creates a reverse dependency, makes the evaluation non-transactional, and bypasses Agent-owned evidence acceptance.

### 5. Map internal reasoning to the existing passive proposal

For checkpoint revision N:

1. Validate snapshot and evidence-view shape and correlation without mutation.
2. Reconcile `ExecutionHypothesis Hn` against the evidence view, producing internal `RuntimeDecision`.
3. If revision is required, check the immutable Strategy adaptation boundary.
4. Stage permitted internal `HypothesisAdaptation` and proposed `Hn+1` inside revision N+1.
5. Return only one existing passive proposal kind:

| Internal result | Agent-facing proposal |
|---|---|
| hypothesis remains supported | `ContinuationSupported` |
| permitted hypothesis revision restores support | `ContinuationSupportedAfterRevision` |
| support requires unavailable evidence or exceeds the accepted boundary | `ContinuationNotSupported` |

`ContinuationSupported` does not authorize work. `ContinuationNotSupported` does not fail the Run. Agent independently selects an existing continuation, recovery, completion, or fail-closed path after validation.

Any Strategy permission that could imply concrete pending-work ordering remains non-operative in this contract. Activating such behavior would require a separately approved Agent-owned hint contract; it cannot be encoded in the passive proposal.

### 6. Preserve transactional commit and extend correlation validation

The existing pre-terminal transaction remains authoritative:

```text
Accepted revision N
    |
    v
Evaluate immutable snapshot + evidence view
    |
    v
Stage proposal and revision N+1
    |
    v
Agent validates and compare-and-accepts
    |
    +-- reject -> discard N+1; accepted history remains N; zero action
    |
    +-- accept -> atomically commit N+1; Agent independently decides Run path
```

In addition to existing pre-terminal checks, Agent validates:

- reasoning session identity and active state
- runtime execution intent reference
- evidence-view contract version and digest
- equality of observation, belief, structural-progress, and trace correlations between snapshot and view
- absence of forbidden evidence fields

The proposal continues to carry no internal RuntimeDecision or HypothesisAdaptation. Those records become visible only in RuntimeAgent-owned accepted history after successful commit.

### 7. The lifecycle is one participant inside one Agent-owned Run

```text
UniAgent authors StrategyDirective
    |
    v
RuntimeAgent admission
    |
    +-- reject -> no Run
    |
    v
RuntimeExecutionIntent
    |
    v
Agent assigns RunId
    |
    v
Create StrategyExecutionReasoningSession + H0 + N0
    |
    v
Agent starts and owns ONE Run
    |
    v
Existing discovery -> Agent authorization -> Traversal execution
    |
    v
Environment Observation -> Agent accepts fresh evidence
    |
    v
WorldBelief update -> structural progress update
    |
    v
Agent creates checkpoint snapshot + evidence view
    |
    v
RuntimeAgent stages internal decision/adaptation and N+1
    |
    v
PreTerminalContinuationProposal
    |
    v
Agent validates
    |
    +-- reject -> discard N+1; existing fail-closed path
    |
    +-- accept -> commit N+1; Agent independently chooses existing Run path
    |
    v
Repeat only when Agent later accepts a new eligible evidence revision
    |
    v
Agent-owned terminal -> seal reasoning session
```

The repeated sequence is the existing Agent Run loop. `StrategyExecutionReasoningSession` is invoked at one checkpoint within it and never calls back into the loop.

### 8. Reuse generic exploration; do not generate DFS policy

The accepted Strategy defines bounded behavior and evidence expectations, not a traversal plan. The existing Agent execution engine remains responsible for dynamic discovery and concrete work selection.

| Generic requirement | Owner relationship |
|---|---|
| discover bounded children | Agent coordinates discovery from accepted evidence; RuntimeAgent does not select a child |
| maintain structural progress | Agent owns progress state; the session consumes its immutable projection |
| evaluate coverage evidence | RuntimeAgent may assess hypothesis support; Agent owns GoalEvidence and completion |
| verify continuity | Agent owns verification; the session consumes accepted continuity evidence references |
| detect contradiction | RuntimeAgent reconciles typed accepted contradiction evidence against the hypothesis |
| execute concrete work | Traversal acts only after Agent authorization |
| enrich evidence | optional upstream capability may contribute before acceptance; this loop neither requires nor invokes it |

Alternative considered: let RuntimeAgent generate DFS policy. Rejected because it would replace the proven execution engine and create target/ordering authority.

Alternative considered: transport a concrete traversal policy from UniAgent. Rejected because Strategy would become a Plan or action sequence and cross the supervisory/execution boundary.

### 9. Separate reasoning convergence from execution completion

The session may internally classify a hypothesis as stable when accepted evidence supports it without revision. Stability is a reasoning property only; it is not exposed as `Completed`, `GoalSatisfied`, `Terminal`, or an equivalent flag.

Agent independently evaluates whether accepted GoalEvidence proves completion. FSM alone performs the lifecycle transition. The session may continue to return a passive support proposal until Agent chooses an existing terminal path; it cannot infer or request that path.

### 10. Select exactly one reasoning mode per Run

At Run admission, composition records one mode:

- `PreTerminalStrategy` for this capability; or
- the existing non-pre-terminal behavior.

The selection is immutable for the Run. When `PreTerminalStrategy` is active, current post-run Phase 2-4 reconciliation and adaptation over the same evidence are suppressed. Terminal handling may seal the session and emit a passive receipt derived from already accepted history, but cannot create new reasoning records.

This guard prevents two ledgers from disagreeing and prevents one accepted evidence history from being interpreted twice.

### 11. Dependency and scope boundary

This change depends on:

- `uniagent-runtimeagent-strategy-contract`
- `runtime-agent-pre-terminal-cycle-contract`

It does not include or depend on:

- a semantic capability boundary or external knowledge package
- scenario migration
- a DFS or Traversal rewrite
- recovery redesign
- mid-Run Strategy alteration
- external escalation transport

Any optional enrichment must already have become accepted typed evidence before this contract sees it. Absence of enrichment is valid and cannot be replaced with inferred meaning.

## Risks / Trade-offs

- [Evidence projection becomes a duplicate World model] -> Keep a minimal closed fact vocabulary, opaque evidence references, and no mutable entities or domain payloads.
- [Reasoning session becomes a hidden controller] -> Give it no Agent, Traversal, FSM, recovery, GoalEvidence, Run-start, or action dependency; only Agent invokes it.
- [Passive proposal is treated as authorization] -> Preserve the existing closed proposal vocabulary and require Agent to make an independent decision after commit.
- [Tentative history leaks before freshness validation] -> Stage N+1 separately and publish only through Agent-authorized compare-and-accept.
- [Pre-terminal and post-run reasoning diverge] -> Freeze one immutable reasoning mode per Run and restrict terminal behavior to sealing/read-only receipt generation.
- [Structural facts encode branch priorities indirectly] -> Forbid actionable entities and ordering fields; facts carry only kind, revision, and evidence reference.
- [Coverage evidence becomes shadow completion] -> Treat coverage only as referenced evidence; prohibit sufficient/complete flags and retain GoalEvidence authority in Agent.
- [External enrichment becomes a hidden dependency] -> Require the loop to operate on available accepted evidence alone and forbid outbound capability invocation.

## Migration Plan

1. Obtain explicit apply approval after this design passes strict OpenSpec validation.
2. Reconfirm both dependency changes and their architecture guards before production edits.
3. Add the immutable evidence projection and run-scoped reasoning session without changing external Strategy admission or the existing passive proposal vocabulary.
4. Bind one session during accepted Strategy Run composition and select `PreTerminalStrategy` as that Run's single reasoning mode.
5. Move Strategy Phase 2-4 reasoning for that mode from post-run evaluation into transactional pre-terminal evaluation; leave terminal handling read-only.
6. Add deterministic lifecycle, transaction, staleness, authority, neutrality, no-external-dependency, and one-mode guards.
7. Run targeted and full regression verification, then have Sol independently decide graduation readiness.

Rollback removes or disables only `PreTerminalStrategy` composition and restores the prior non-pre-terminal reasoning path. Existing Strategy payloads, Agent execution ownership, lifecycle protocol, Traversal, Environment, and external operations require no migration.
