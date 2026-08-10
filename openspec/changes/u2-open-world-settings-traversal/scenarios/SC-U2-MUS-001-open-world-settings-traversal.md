# SC-U2-MUS-001 — Bounded Open-World Settings Traversal

> U2 minimum usable Agent slice | Semantic status: `HUMAN_FROZEN`
> Development lane: `CAPABILITY_DELIVERY_FAST`
> Evidence target: `L2_SHORT_CHAIN_INTEGRATION`

## Goal

From the authoritative high-level task intent
`遍历 Settings 中 depth <= 1 的所有安全配置项`, execute a truthful
`OPEN_WORLD_TYPE_LEVEL` representation through runtime discovery, verified
parent return, sibling continuation, and Agent-owned traversal completion
evidence without pre-enumerating a concrete route.

## Given

- A resolved `IntentSemanticEnvelope` contains the existing Goal and a validated
  `TypeLevelTraversalSpecification`:
  - scope/entry: `Settings / SettingsRoot`;
  - target category and safety: navigable Containers only;
  - maximum semantic depth: `1`;
  - completion: exhaustive within the declared scope.
- Fresh root evidence proves the complete in-scope sibling inventory `{A, B}`
  and also exposes one dangerous state-changing candidate.
- A and B are absent from any pre-execution concrete Plan.
- Each child exposes exactly one explicit parent target named `SettingsRoot`.
- A child may expose a deeper navigable candidate, which lies outside depth 1.

## Positive Oracle

```text
fresh root inventory {A, B}
→ authorize and dispatch A exactly once
→ fresh A evidence proves bounded terminal state
→ authorize unique SettingsRoot return exactly once
→ fresh exact parent reconciliation
→ record A while B remains pending
→ authorize and dispatch B exactly once
→ fresh B evidence proves bounded terminal state
→ authorize unique SettingsRoot return exactly once
→ fresh exact parent reconciliation
→ no unresolved in-scope work and no parent frame remains
→ derive VerifiedBoundedTraversalCompletion
→ invoke existing Goal.EvidenceEvaluator on fresh root evidence
→ satisfied fresh GoalEvidence
→ Agent sets Completed
```

Required positive proof:

1. no concrete Plan, future page list, target list, coordinate, route, or work
   inventory is manufactured;
2. every action is selected/dispatched/freshly verified by Traversal;
3. A remains complete while B remains pending and no early completion occurs;
4. dangerous and beyond-depth candidates receive zero dispatch;
5. exactly four Tap actions occur: enter A, return, enter B, return;
6. final completion comes only from the conjunction of Agent-derived verified
   bounded traversal completion and satisfied existing fresh GoalEvidence;
7. Trace uses bounded-scope/cutoff language and never claims whole-world or
   discovered-world exhaustion.

## Negative Oracles

- unresolved complete inventory → zero discovered-branch dispatch and no final
  Goal evaluation;
- A complete while B remains rejected/unresolved → no early Goal evaluation and
  no completion;
- zero/multiple/rejected parent target → zero return dispatch, no child
  completion, no blind redispatch;
- return dispatch followed by wrong parent evidence → no child completion and
  explicit failure;
- stale/failed post-action Observation → no terminal/exhausted classification
  and no final Goal evaluation;
- verified bounded traversal with unsatisfied existing fresh GoalEvidence →
  explicit Failed, not mechanical success.

## Deterministic Replay

Equal RunId, resolved envelope, criteria, Fake world, and action outcomes must
produce equal ActionHistory, ObservationHistory, Traversal journal, Agent Trace,
BranchProgress, GoalEvidence receipts, and final RunState.

## Frozen Boundaries

- `VerifiedBoundedTraversalCompletion` is a semantic condition, not a new type.
- Intermediate progress, visited known nodes, local branch exhaustion,
  observation failure, ambiguity, and depth/safety cutoff are not global
  completion.
- Non-traversal desired-world-state Goal completion remains unchanged.
- Agent retains global semantic/progress/completion authority; Traversal retains
  local selection/execution/fresh verification; Container and Environment retain
  their frozen ownership.
- No Planner, Compiler engine, FSM, Graph, route/frontier model, new Back action,
  generic retry/uncertainty, Recovery change, or architecture change is in scope.
