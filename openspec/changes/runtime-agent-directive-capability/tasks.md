# Tasks: runtime-agent-directive-capability

> Implementation checklist. Each task is verifiable against
> `specs/runtime-agent-directive-decomposition/spec.md`. Order respects dependencies:
> model → decomposer → seam entry → tests → regression → validate.

## 1. Bounded exploration directive model

- [x] 1.1 Create `src/UniClaw.Runtime/Model/Directive.cs`: sealed record `Directive`
      carrying `TypeLevelTaskScope Scope`, `TypeLevelEntryBoundary Entry`, `int
      MaximumDepth`, `TypeLevelSafetyBoundary Safety`,
      `TypeLevelCompletionRequirement Completion`, and a `DirectiveStrategyRules`
      record (candidate-authorization, branch-inventory, viewport-exploration,
      category-classifier evaluator delegates) + optional `TypeLevelDispatchPolicy`.
- [x] 1.2 Add construction-time validation mirroring `TypeLevelTraversalSpecification`:
      non-null scope/entry/safety, non-empty safety, depth >= 0, completion ==
      ExhaustiveWithinScope; reject empty/invalid with `ArgumentException`.
- [x] 1.3 Assert the `Directive` exposes NO `Plan`, no coordinates, no `DeviceAction`,
      no element index (model-level immutability test in task 4).

## 2. Stateless directive decomposer

- [x] 2.1 Create `src/UniClaw.Runtime/Planning/DirectiveDecomposer.cs`: `static` class
      with `Decompose(Directive)` returning a `DirectiveDecompositionResult` union
      (`Resolved(TypeLevelTraversalSpecification, Goal)` | `Insufficient(reason)`).
- [x] 2.2 Project the `Directive` boundary fields 1:1 into a
      `TypeLevelTraversalSpecification` (scope, entry, depth, safety, completion,
      dispatch policy). Derive nothing beyond the caller's boundary.
- [x] 2.3 Project the caller-injected strategy rules 1:1 onto a `Goal`
      (CandidateAuthorizationEvaluator, BranchInventoryEvaluator,
      ViewportExplorationEvaluator, CategoryClassifier). Never synthesize a rule.
- [x] 2.4 Return `Insufficient` (no execution inputs, no fabricated rule) when a rule
      required by the declared completion requirement is missing.

## 3. Additive execution entry (reuse existing seam)

- [x] 3.1 Add a bounded execution entry that wraps the decomposed spec in an
      `IntentSemanticEnvelope.Resolved` + `IntentExecutionRepresentation.OpenWorldTypeLevel`
      and the decomposed `Goal`, then calls `IntentExecution.RunOpenWorldAsync`. Place
      it in `Planning/` (sibling to `IntentExecution`) or extend `IntentExecution`
      additively — do NOT add a new `Agent` public method.
- [x] 3.2 Confirm `Agent.RunOpenWorldAsync`, `Agent.cs`, `Container/`, `Traversal/`,
      `Recovery/`, `World/` are byte-unchanged (diff review).

## 4. Deterministic tests

- [x] 4.1 `DirectiveTests`: construction exposes only task-level declarations; rejects
      empty safety, negative depth; no Plan/coordinates/DeviceAction exposed.
- [x] 4.2 `DirectiveDecomposerTests`: valid directive → spec shape (scope/entry/depth/
      safety/completion/dispatch) + Goal evaluators match caller rules; deterministic
      (two decompositions structurally equal); world-free (no observation invoked).
- [x] 4.3 `DirectiveDecomposerAuthorizationTests`: a candidate the caller's rule
      rejects stays rejected after decomposition; no synthesized authorization;
      forbidden category stays forbidden.
- [x] 4.4 `DirectiveDecomposerAuthorityTests`: decomposer holds no mutable state and
      participates in no decision; the run still routes through
      `Agent.RunOpenWorldAsync` (RuntimeAgent keeps sole authority) — assert via a
      Fake-environment end-to-end that the existing DFS path executes.
- [x] 4.5 `DirectiveDecomposerInsufficientTests`: missing required rule →
      `Insufficient` result, no execution inputs, no fabricated rule.

## 5. Regression guard

- [x] 5.1 Run `dotnet build src/UniClaw.Runtime.sln` — 0 errors, 0 warnings.
- [x] 5.2 Run `dotnet test src/UniClaw.Runtime.sln` — all existing suites green,
      including SETTINGS-TREE-01 capstone (TREE-1..TREE-20), SC-U2-MUS-001, SC-OW-TD-001,
      bounded candidate safety, cross-page discovery.
- [x] 5.3 Confirm ArchitectureGuardTests pass (Guard 1: zero ProjectReference; Guard 2:
      no legacy namespace) and `scripts/check-consistency.sh` passes.

## 6. OpenSpec validate

- [x] 6.1 Run `openspec validate runtime-agent-directive-capability --strict` — passes.
- [x] 6.2 Update this `tasks.md` checkbox state as each task completes.

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Runtime/Planning/` | [docs/system/layers/planning.md](../../../docs/system/layers/planning.md) |
| `src/UniClaw.Runtime/Agent/` (execution authority, unchanged) | [docs/system/layers/agent-runtime.md](../../../docs/system/layers/agent-runtime.md) |
| `src/UniClaw.Runtime/Model/` (immutable models) | [docs/system/greenfield-runtime-charter.md](../../../docs/system/greenfield-runtime-charter.md) §40 + `src/UniClaw.Runtime/AGENTS.md` directory table |
