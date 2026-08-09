## Context

Agent owns Plan, Goal, WorldBelief, active Container transitions, high-level decisions, GoalEvidence consumption, and final RunState. Container owns one semantic page and its local progress; `Bind` resets `ExecutedSteps` and `IsLocalComplete`. Traversal owns an append-only local execution journal. Environment owns only external observations and dispatch outcomes. Recovery owns restore/observe/verify mechanics and no higher-level progress authority.

The current fixed `Plan` is a hypothesis and may provide the bounded Scenario's approved A/B navigation actions, but it cannot be used as proof that those branches exist or completed. `GoalEvidence` can prove final completion from world evidence but is not persistent cross-Container progress state. No accepted production surface associates proven child completion with a parent scope or preserves that evidence while another sibling is visited.

SC-P3-CAND-004 therefore requires one new immutable progress-evidence value and one Agent-owned state field. The S0 Capstone's autonomous branch discovery and autonomous forbidden-candidate classification remain separate roadmap pressures; this change proves only the bounded branch-progress distinction.

## Goals / Non-Goals

**Goals:**

- Represent one bounded parent scope's approved sibling inventory and proven sibling completions as immutable evidence.
- Associate every branch fact with the correct parent semantic identity and source Observation sequence.
- Preserve A's valid completion while returning to P and visiting B.
- Prevent a parent/subtree completion verdict while any approved sibling lacks proof.
- Prevent parent revisits, child revisits, fresh Observations, or action dispatch from fabricating distinct completion.
- Reject stale, absent, or conflicting identity evidence.
- Preserve Agent ownership and final GoalEvidence authority.
- Replay progress, observations, actions, journal, Trace, GoalEvidence, and RunState deterministically.

**Non-Goals:**

- Implement SC-S0-CAPSTONE-001 or generalized autonomous traversal.
- Classify arbitrary newly discovered dangerous actions.
- Add a graph, stack, tree, hierarchy, visited-set semantic type, navigation/progress manager, TraversalContext, ResumeToken, FSM, workflow engine, new Back action, or Container hierarchy.
- Decide whether pre-drift progress remains valid after Recovery.
- Change Recovery ownership, Recovery dependencies, Container local ownership, or final completion authority.
- Refactor Agent/Container/Traversal structure.

## Decisions

### Add one immutable branch-progress evidence value

The approved model value has exactly three semantic fields:

1. parent semantic identity;
2. complete approved sibling-inventory evidence for that bounded parent scope;
3. proven sibling-completion evidence.

Inventory and completion evidence associate semantic branch identity with a source Observation sequence. The concrete immutable collection representation is an implementation choice constrained by the approved one-type/three-field budget. Completed sibling identities must be a subset of the approved sibling inventory. No boolean “all complete”, status enum, graph node, or mutable collection is stored; bounded subtree completion is derived from evidence coverage.

Alternative rejected: reuse a Plan index or executed-step count. Those are execution hypotheses/mechanics and cannot prove parent association, sibling inventory, or world-backed completion.

Alternative rejected: add a visited-page set. A visit is not completion, and an unscoped set cannot distinguish the same semantic child under different parent scopes or carry completion evidence.

### Agent remains the single cross-Container progress owner

Agent receives exactly one new state field holding immutable progress snapshots indexed by parent semantic identity. Updating progress replaces an immutable value; no mutable collection crosses an owner boundary. Container continues to own its local Observation, executed steps, and local completion. Traversal continues to own only its journal.

This is no ownership or authority transfer. The Charter already assigns Plan, active Container management, high-level decisions, and high-level completion conditions to Agent.

Alternative rejected: store sibling progress in each Container. Returning through recreated/rebound Containers would duplicate or lose higher-level state and create competing owners.

Alternative rejected: derive semantic completion directly from Traversal journal. Traversal has no semantic subtree authority.

### Establish inventory only from fresh bounded parent evidence

The bounded formal Scenario exposes approved child affordances A and B in parent P. Agent may reconcile those candidates with the Scenario-approved traversal boundary, but the fixed Plan does not prove their existence. The inventory evidence is accepted only from a fresh Observation whose semantic identity is P and whose visible bounded scope proves the complete approved A/B sibling set.

This decision does not solve autonomous candidate safety or claim that every arbitrary Settings page exposes its full inventory in one viewport. SC-P3-003 may supply same-Container viewport evidence where later Scenarios require it.

### Record child completion before parent return

A child branch may be recorded complete only when the active child Container already has valid local-completion evidence before executing the approved parent-return step, and the subsequent fresh Observation reconciles to the correct parent P. The return action, dispatch result, or parent Observation does not itself prove child completion.

Recording completion is idempotent by semantic child identity within its parent scope. Revisiting A can refresh evidence only when the same rules prove completion; it cannot create another distinct branch or increase coverage.

Alternative rejected: mark a child complete whenever navigation returns to P. A return can occur before child work completes and would fabricate progress.

### Derive parent completion; preserve final Agent authority

The bounded parent/subtree is locally complete only when fresh approved-inventory evidence exists and every approved sibling identity has valid completion evidence. A complete child with an unvisited sibling leaves the parent incomplete. Stale/absent evidence or a parent/child identity conflict forbids the update and cannot be silently attached to another scope.

This derived bounded-subtree fact is still not final Goal completion. Only Agent evaluation of `GoalEvidence` may set `RunState.Completed`.

### Parent return uses existing action semantics

The deterministic world exposes a visible approved parent-return affordance that existing Tap can target. Parent return is ordinary execution mechanics inside SC-P3-CAND-004. No new Back action or navigation abstraction is required.

### Recovery-progress validity remains separate

Agent-owned progress can remain present in memory while Recovery executes, but successful world recovery does not prove that pre-drift inventory or completion evidence is still valid. No recovery-resume behavior is added. A later research result or bounded Candidate must decide revalidation/invalidation.

### Preserve current structure despite pressure

The Agent's control flow is already under structural pressure. This change may add only the minimum private mechanical helpers needed to update the approved evidence value. It must not extract a new component or refactor the execution pipeline. Ownership remains unambiguous, so no Architecture Review is required by this design.

## Risks / Trade-offs

- [Risk] The bounded inventory rule does not yet generalize to autonomous multi-viewport discovery. → Mitigation: keep SC-P3-CAND-004 independent and leave the Capstone's broader discovery/safety pressure explicit.
- [Risk] Container's current local-completion surface is intentionally small. → Mitigation: formal fixtures require child-local proof before parent return and prohibit return itself from becoming completion evidence.
- [Risk] Similar semantic page names could attach evidence to the wrong parent. → Mitigation: require reconciled parent identity and reject conflicting evidence; do not add a new identity framework.
- [Risk] Adding Agent-owned state increases structural pressure. → Mitigation: one immutable value and one field only; defer extraction until additional S0 evidence proves a seam.
