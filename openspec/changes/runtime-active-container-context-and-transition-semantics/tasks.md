## 1. Apply Human Gate and source revalidation

- [ ] 1.1 Obtain explicit Human authorization for Apply; proposal creation alone does not authorize any production or test change
- [ ] 1.2 Re-run the BEFORE inventory against then-current source and stop if the state count, owners, active OpenSpec contracts, or dirty-worktree overlap differs materially from `design.md`
- [ ] 1.3 Freeze focused deterministic fixtures for all seven buyer cases plus normal-path before/after replay evidence before modifying production code

## 2. Stage A — Behavior-neutral semantic seam

- [ ] 2.1 Add the minimal immutable transition kind/disposition/result model and pure classification/prepare seam without an Agent latest-transition field
- [ ] 2.2 Replace the mutable stability-detail string handoff with an immutable operation result while preserving every non-location quiescence classification and terminal reason semantically
- [ ] 2.3 Emit immutable structured transition evidence and add the authority-free Runtime/DriverHost read projection with explicit unavailable behavior for older runs
- [ ] 2.4 Prove `SAME_CONTAINER`, authorized child entry, verified return, sibling continuation, and authorized external-boundary behavior remain semantically equivalent with zero action/control-flow delta

## 3. Stage B — Ownership consolidation

- [ ] 3.1 Add the structurally immutable, two-field `ActiveContainerContext` as the sole Agent-owned active execution-context slot
- [ ] 3.2 Migrate every `_activeContainer` read/write across PlanRun, SemanticRun, OpenWorld, and existing Recovery mechanics, then delete `_activeContainer`
- [ ] 3.3 Move the exact existing parent Container and entered-child obligation values from the method-local parent stack into ordered `ActiveAncestorPath`, then delete the stack
- [ ] 3.4 Derive ancestry membership and semantic depth from the path plus active execution Container, delete the mutable ancestry slot, and preserve the separate run-local visited evidence
- [ ] 3.5 Add structural/architecture tests proving semantic mutable facts are 4→3, mutable storage slots are 4→2, owners are 1→1, and no old/new dual track remains

## 4. Stage C — Atomic unexpected-transition reconciliation

- [ ] 4.1 Implement the validation-before-commit bundle and one synchronous no-I/O/no-await Agent commit seam for belief, context, permitted existing progress replacement, Container observation acceptance, and transition event
- [ ] 4.2 Prove rollback/no-commit for inconsistent context, failed classification, stale evidence, and failed exact-parent/continuity validation
- [ ] 4.3 Implement the r5 buyer so fresh accepted `SettingsRoot` updates WorldBelief while `Display` remains the incomplete active execution obligation, with zero automatic completion, recovery, re-entry, or action
- [ ] 4.4 Implement and verify known non-parent, external exit, and accepted Unknown transition branches without destination, recovery, route, or completion authorization
- [ ] 4.5 Preserve existing verified-return, BoundaryRelation, BranchProgress, Container-local completeness, GoalEvidence, and Recovery contracts without copied state

## 5. Read model and Debug Toolchain handoff

- [ ] 5.1 Project observed location, active execution Container, active ancestor path, latest committed transition, CompletenessRef, and EvidenceRef as immutable truth-source-classified snapshots
- [ ] 5.2 Correlate transition FreshObservationRef to existing EvidenceRef and AssetRef indexes, reporting missing assets explicitly and embedding no asset bodies
- [ ] 5.3 Create a separate Human-gated Runtime Debugging Toolchain slice for `container context`, `container transitions`, `container transition`, and the TUI panel; do not implement CLI/TUI in this change

## 6. Verification and lifecycle

- [ ] 6.1 Run focused Unit/Scenario tests for all transition kinds, atomic rollback, state replacement, ancestry cycle/visited behavior, completeness non-copying, and read-only projection
- [ ] 6.2 Run `dotnet build src/UniClaw.Runtime.sln`, `dotnet test src/UniClaw.Runtime.sln`, Architecture Guards, and `scripts/check-consistency.sh`, recording independent environment/trust blockers separately
- [ ] 6.3 Run `openspec validate runtime-active-container-context-and-transition-semantics --strict` and verify no active OpenSpec links to WorkItems or unauthorized Runtime Debugging implementation
- [ ] 6.4 Produce a Human verification packet answering the ten gate questions with current code/test evidence; do not archive or claim graduation without explicit Human lifecycle authorization

## Design Docs

> Auto-generated from proposal Impact section and refined for this repository.
> Implementation agents: read these before starting.

| Module | Design Doc |
|---|---|
| `src/UniClaw.Runtime/Agent/` | `openspec/changes/runtime-active-container-context-and-transition-semantics/design.md` + `docs/system/layers/agent-runtime.md` |
| `src/UniClaw.Runtime/Container/` | `docs/system/layers/container-runtime.md` |
| `src/UniClaw.Runtime/Model/` | `docs/system/patterns/state-and-belief-model.md` + this change's specs |
| `src/UniClaw.Runtime.DriverHost/` | `docs/system/patterns/observability-and-results.md` + `openspec/changes/runtime-debugging-toolchain/design.md` |
| `tests/UniClaw.Runtime.Tests/` | `tests/UniClaw.Runtime.Tests/AGENTS.md` |
