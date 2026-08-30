# RuntimeAgent Plan Hypothesis — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_RUNTIME_AGENT_PLAN_HYPOTHESIS` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-runtime-agent-plan-hypothesis/`
> Authority: Runtime Architecture Contract I-1..I-14 and Architecture v1 remain the governing baselines; this decision adds no architecture authority.

## 1. Buyer and exact claim boundary

**Buyer:** RuntimeAgent run-local execution-hypothesis observability — the mission proof goal stated in proposal.md's Why: "RuntimeAgent can maintain and revise a run-local execution hypothesis without gaining new authority" (post-Phase 1 `runtime-agent-directive-capability`).

This receipt claims only that:

1. the immutable `ExecutionHypothesis` record and `ExecutionHypothesisStatus` lifecycle (Created → Active → Confirmed | Revised → Replaced) exist in `Model/` and carry only assumption fields — RunId, DirectiveReference, Objective, ExpectedTransition, ExpectedOutcome, Confidence, RevisionReason, CreatedAtObservation, Status — with no Plan, element coordinates, DeviceAction, TraversalStep, element index, scenario strings, authorization rules, or completion authority;
2. the run-local, method-local `ExecutionHypothesisLedger` in `Planning/` creates the initial hypothesis from a decomposed directive's declared scope, maximum depth, and completion requirement (no scenario strings), and revises the hypothesis sequence post-run from `Agent.Trace` inflection points + the `RunState` outcome; it is a transient derivation discarded when the run method returns, never Runtime state owned by Agent/Container/Traversal/Environment;
3. `DirectiveExecution.RunDirectiveAsync` integration is additive — an optional nullable `ExecutionHypothesisLedger?` parameter defaulting to null, with null = existing Phase 1 behavior, zero regression;
4. the DFS engine is unmodified: `Agent.OpenWorld.cs`, `Agent.cs`, `Agent.Recovery.cs`, `Container/`, `Traversal/`, `Recovery/`, `World/`, `IntentExecution.cs` are byte-unchanged, and the DFS loop does not reference the hypothesis or ledger;
5. the hypothesis and ledger hold no decision, authorization, completion, or execution authority: the Agent remains the sole run-level semantic and execution authority, the RunState is produced by the existing DFS engine, the GoalEvidence is evaluated by the existing evidence evaluator, and no recursive authority (dispatch / container creation / sub-run) exists.

No claim is made for: real-time mid-loop hypothesis revision (observer pattern in the DFS loop); Agent-observable hypothesis state (an Agent field/property); wiring the hypothesis into the closed-world `RunSemanticGoalAsync` path or the `RunStartRequest` wire surface; global plan store, persistent hypothesis, navigation graph, scenario knowledge, or LLM planning; or any change to Architecture v1, Protocol v1, Contract I-1..I-14, the charter, the `Agent.RunOpenWorldAsync` signature, or any frozen decision.

## 2. Validation evidence

The change contains no `evidence/` directory; the recorded verification record is the completed `tasks.md` checklist plus the `proposal.md` baseline. Every item below is what the change's files record:

- `proposal.md` records the baseline: "Baseline verified: 2026-08-21, branch `uni-agent`, build 0 errors / 0 warnings, 1506 deterministic tests green", and "Authority decision: Leader review passed — NONE authority impact. The hypothesis is a passive, run-local, trace-derived record; the Agent keeps sole authority; the DFS engine is unchanged."
- `tasks.md` records §1–§6 complete: the model, lifecycle, ledger, run-local isolation, authority, and boundary-revision scenario test classes are all implemented and checked off — `ExecutionHypothesisTests`, `ExecutionHypothesisLifecycleTests`, `ExecutionHypothesisLedgerTests`, `ExecutionHypothesisRunLocalIsolationTests`, `ExecutionHypothesisAuthorityTests`, `ExecutionHypothesisBoundaryRevisionScenarioTests`.
- `tasks.md` records task 7.1 complete: "Run `dotnet build src/UniClaw.Runtime.sln` — 0 errors, 0 warnings."
- `tasks.md` records task 7.2 complete: "Run `dotnet test src/UniClaw.Runtime.sln` — all deterministic suites green (1506+), including SETTINGS-TREE-01 capstone (TREE-1..TREE-20), U2OpenWorld, OpenWorldTypeDirected, BoundedCandidateSafety, BoundedCrossPageDiscovery, Phase 1 directive tests, ArchitectureGuardTests. Only pre-existing env-gated RealDevice/RealEmulator tests may fail (no emulator in sandbox)."
- `tasks.md` records task 7.3 complete: "Confirm `scripts/check-consistency.sh` ALL PASS and `git diff --check` clean."
- `tasks.md` records task 8.1 complete: "Run `openspec validate runtime-agent-plan-hypothesis --strict` — passes."
- `tasks.md` records task 3.3 complete: `Agent.OpenWorld.cs`, `Agent.cs`, `Agent.Recovery.cs`, `Container/`, `Traversal/`, `Recovery/`, `World/`, `IntentExecution.cs` byte-unchanged (diff review).
- `design.md` documents the HOW via Decisions 1–5: immutable record in `Model/` (Decision 1); method-local ledger respecting the Planning layer mandate "Planning owns no mutable Runtime state" (Decision 2); post-run trace-derived revision with zero DFS modification (Decision 3); optional nullable parameter with zero-regression default (Decision 4); objective/transition derived from the directive's declared boundaries with no scenario strings (Decision 5). The Migration Plan records rollback as: delete the two new files and revert the one optional-parameter addition.

## 3. Scenario receipts and falsifiers

The change files record no explicit falsifier section (no `evidence/` directory; design.md's "Risks / Trade-offs" entries carry mitigations, not falsifier proofs); rejection/negative requirements are defined in `specs/runtime-agent-plan-hypothesis/spec.md`:

- **Execution hypothesis representation** — "The record MUST NOT carry a `Plan`, element coordinates, a `DeviceAction`, a `TraversalStep`, an element index, scenario strings, authorization rules, or any completion authority." Recorded outcome: model-level surface assertions complete (tasks.md 1.3, 4.1).
- **Run-local hypothesis ledger** — "The ledger MUST NOT survive as global memory, cross-run knowledge, or a navigation model. The ledger MUST NOT be Runtime state owned by Agent, Container, Traversal, or Environment; it is a transient, method-local derivation." Recorded outcome: method-local-by-construction and run-local isolation tests complete (tasks.md 2.4, 4.4).
- **No authority over execution** — "The hypothesis and ledger MUST NOT acquire any decision, authorization, completion, or execution authority. The hypothesis MUST NOT be consulted by the Agent for decisions, authorization, completion, or execution. The ledger MUST NOT call any Agent method that mutates run state, authorizes an action, evaluates GoalEvidence, or dispatches a DeviceAction. The RuntimeAgent MUST remain the sole run-level semantic and execution authority; the DFS engine MUST be unchanged." Recorded outcome: authority tests complete — no authorize/bypass/completion/recursive-authority methods, RunState produced by the DFS engine, GoalEvidence evaluated by the existing evaluator (tasks.md 5.1–5.4, 6.1).
- **Additive integration without DFS modification** — "When the parameter is absent (null), the existing Phase 1 behavior MUST be preserved with zero regression. The DFS engine (`Agent.RunOpenWorldAsync`), the `IntentExecution` seam, `Agent.OpenWorld.cs`, `Agent.cs`, `Container/`, `Traversal/`, `Recovery/`, and `World/` MUST remain unchanged." Recorded outcome: optional-parameter integration and byte-unchanged diff review complete; Phase 1 directive and open-world suites recorded green (tasks.md 3.1–3.3, 7.2).

## 4. Deferred scope

- Real-time mid-loop hypothesis revision (would require an observer pattern / DFS-loop modification) — out of scope; post-run trace-derived revision satisfies the proof goal (design.md Non-Goals).
- Agent-observable hypothesis state (adding a field to Agent); the hypothesis is observed via the ledger returned/retained by the caller, not an Agent property (design.md Non-Goals, Risk trade-off).
- Wiring the hypothesis into the closed-world `RunSemanticGoalAsync` path or the `RunStartRequest` wire surface (design.md Non-Goals).
- Global plan store, persistent hypothesis, navigation graph, scenario knowledge, LLM planning — explicitly forbidden by the mission and the frozen invariants (design.md Non-Goals).

## 5. Final conclusion

**GRADUATED.** The bounded claim — an immutable, run-local execution hypothesis model; a method-local, authority-free revision ledger; an additive zero-regression `DirectiveExecution` integration; an unmodified DFS engine — is recorded as implemented and verified (tasks.md build/test/consistency/validate checkboxes complete on the proposal.md 2026-08-21 baseline, Leader review: NONE authority impact). Archival is performed on 2026-08-30 as a separate lifecycle operation in this batch.