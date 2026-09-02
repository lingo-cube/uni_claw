## 1. Freeze, authority, and Apply boundary

- [x] 1.1 Publish `CONTAINER_GRAPH_PREVIOUS_PURCHASE_FROZEN` and the old-purchase / Phase 2.6 / V2 reconciliation ledger without deleting historical implementation or evidence
- [x] 1.2 Record the Human direction as authorization for staged, reversible Container Runtime V2 Apply while preserving separate gates for provider/backend purchase, upper-layer authority changes, graduation, and archive
- [x] 1.3 Revalidate the dirty working tree, current owners, source overlap, and old active change before each behavior-changing stage; stop rather than overwrite unrelated edits
- [x] 1.4 Keep a stage result with `STATUS / PURCHASED / HYPOTHESIS / IMPLEMENTED / VALIDATED / DEFERRED / RISKS / NEXT_WORKITEM`

## 2. Immutable V2 core model — first Apply slice

- [x] 2.1 Add the minimum opaque Run-local refs and immutable node, relation, Slice, EntryContext, CurrentContainer, TransitionOccurrence, Graph snapshot, and aggregate state contracts in the existing Runtime Model responsibility
- [x] 2.2 Add a pure prepare/reduce seam that validates evidence revision and structural references, returns an immutable next state or explicit no-commit rejection, and performs no observation, action, recovery, provider, completion, or I/O work
- [x] 2.3 Prove working unproven node creation, transition-completed-before-trust, same Destination with distinct relations, off-path occurrence without normal relation, r5 current-location/obligation separation, and atomic stale/invalid rollback
- [x] 2.4 Add architecture guards proving no planner/action/recovery/completion APIs, canonical parent, mutable latest transition/trust/checkpoint, Graph-current slot, existing Agent behavior change, or second current-location truth
- [x] 2.5 Record `NEW_SYMBOL_JUSTIFICATION` for every created type and show why existing `Container.SemanticPageName`, old `ContainerTransition`, and DriverHost `RunExecutionGraph` cannot own the new responsibility

## 3. Evidence-only Graph read/record seam

- [x] 3.1 Search all existing Graph/read-model/semantic-store abstractions again and add only the independently owned Graph read and record seams that cannot be satisfied by extension
- [x] 3.2 Implement a Run-local in-memory evidence Graph using immutable snapshot replacement or append-only records; expose derived relation assessments and no route/action API
- [x] 3.3 Prove Desktop/Search multi-entry to one Settings node yields two relations and two EntryContexts, repeated occurrences may support one relation, equal trigger text does not merge relations, and historical relation loses to fresh evidence
- [x] 3.4 Prove abnormal/transient/off-path occurrences remain readable but do not become normal reusable relations without an explicit non-authoritative eligibility assessment

## 4. CurrentContainer and TransitionOccurrence migration

- [ ] 4.1 Adapt old immutable TransitionRef/EvidenceRef/AssetRef history to the V2 occurrence contract without treating old kinds/dispositions as Graph truth
- [ ] 4.2 Introduce the sole Agent-owned CurrentContainer state and atomically commit fresh accepted physical location, EntryContext, occurrence, permitted local evidence, and permitted existing progress evidence
- [ ] 4.3 Move pending execution obligation and active-path evidence out of current-location semantics; retain compatibility projections only until all readers migrate, then delete the old ActiveContainer current authority
- [ ] 4.4 Preserve verified return, external-boundary evidence, BranchProgress, GoalEvidence, recovery, sibling selection, and normal-path action authorization without copying state
- [ ] 4.5 Prove path-relative return for same node/different source, `ENTRY_RELATION != RETURN_RELATION`, `RETURN_EXPECTATION != RETURN_TRUTH`, unexpected destination current commit, and no automatic recovery/re-entry/completion
- [ ] 4.6 Update the authority-free DriverHost/read projection to expose V2 current, entry, occurrence, evidence revision, assessment availability, and explicit missing historical fields

## 5. Slice, LocalModel, identity safety, and coverage

- [ ] 5.1 Reuse existing Container current observation and accepted viewport history as CurrentSlice and node-lifecycle LocalModel inputs; create no parallel mutable local model
- [ ] 5.2 Preserve combined bounded correlation while guarding `BOUNDS/TEXT/ORDINAL/STABLEKEY/GEOMETRY != ITEM_IDENTITY` and `LOCAL_MODEL_ITEM != CURRENT_ACTION_OCCURRENCE`
- [ ] 5.3 Replace run-global duplicate semantic-page rejection with relation/obligation-aware safety so same-node/different-source is legal while unauthorized active-path loops remain fail-closed
- [ ] 5.4 Extend existing completeness evidence/projection to separate coverage, semantic resolution, subtree completion, and Goal completion without a completeness FSM
- [ ] 5.5 Prove overlap/gap/no-overlap scroll cases, cross-Container label/StableKey isolation, stale LocalModel bounds rejection, one empty delta not complete, and coverage-complete with Unknown still semantically unresolved

## 6. Fast Container resolution and derived trust

- [x] 6.1 Reuse the existing Fast Semantic candidate/evidence/fusion contracts and add a single Fast Container resolver seam only after search proves the responsibility is independent
- [x] 6.2 Combine immutable action prior, fresh Slice, trigger-destination semantics, existing Graph candidates, independent-boundary support, and hard conflict into a revision-bound assessment
- [x] 6.3 Derive Fast Trust without mutable trust state and preserve `FAST_TRUSTED != ACTION_AUTHORIZATION/COMPLETE/SLOW_CONFIRMED/PUBLISHED_MEMORY`
- [x] 6.4 Prove SAME/NEW/TRANSIENT/AMBIGUOUS, trigger-destination support, Graph candidate ranking, hard-conflict precedence, vector-not-truth, latency bound, and no behavior when the resolver abstains

## 7. Slow Semantic Advisor Disabled/Shadow seam

- [x] 7.1 Add the provider-neutral Slow Advisor request/result/mode contract with exact Observation/Node/Source/Trigger/Transition evidence revision binding and `NEW_SYMBOL_JUSTIFICATION`
- [x] 7.2 Implement Disabled and Shadow consumption first; Shadow records immutable assessment evidence and has zero CurrentContainer, Graph, action, recovery, planning, completion, or Goal behavior effect
- [x] 7.3 Add stateful async tests for Fast-first, Slow confirm/challenge/correct/insufficient, stale Slow result, fresh revision precedence, advertisement/transient/overlay/off-path assessment, and false-correction visibility
- [x] 7.4 Define experiment metrics and falsifiers for correction precision, blocker reduction, false correction, latency, and cost before any concrete provider/backend purchase

## 8. Semantic correction to UniAgent obligation boundary

- [x] 8.1 Add immutable correction facts bound to exact evidence refs and a read-only Runtime-to-UniAgent obligation input; do not add Slow command/action/recovery APIs
- [x] 8.2 Prove traversal mis-click correction leaves intended C pending and records actual D visited only through UniAgent obligation authority
- [x] 8.3 Prove directed-entry wrong branch produces a correction/proposal and requires separately authorized return/recovery/re-entry
- [x] 8.4 Expose checkpoint only as a derived proposal after sufficiently confirmed path evidence; keep production checkpoint state/recovery behavior deferred
- [x] 8.5 Stop for a fresh Human Gate if completing this stage requires changing frozen Goal, Agent, Driver, Environment, external protocol, or product-level provider/dependency authority

## 9. Verification and regression gates

- [x] 9.1 Run focused Unit/Architecture/Scenario tests for each completed stage and independently inspect code rather than accepting Worker claims
- [x] 9.2 Run `dotnet build src/UniClaw.Runtime.sln`, `dotnet test src/UniClaw.Runtime.sln`, Architecture Guards, and `scripts/check-consistency.sh`; classify environmental/configuration failures separately
- [x] 9.3 Run `openspec validate container-runtime-v2-core-semantics --type change --strict --no-interactive` and verify active OpenSpec does not link WorkItems
- [ ] 9.4 Produce the final KEEP/MOVE/DELETE/DEFER symbol map, BEFORE/AFTER ownership proof, and `NET_NEW_MUTABLE_TRUTH = 0` evidence from implemented code

## 10. Fresh Phase 2.6 acceptance and lifecycle

- [x] 10.1 Prepare deterministic E2 and stateful async E3 fixtures for r5, multi-entry return, same-node/different-source, working unknown destination, off-path occurrence, Fast/Slow conflict, wrong branch, coverage+Unknown, and stale bounds
- [ ] 10.2 Run a fresh real-device Phase 2.6 campaign and measure completion/depth, blocker migration, wrong-branch correction, deep Unknown, false identity/trust, LocalModel stability, fresh grounding, latency/cost, and recovery cost
- [ ] 10.3 Compare Fast-only, Fast+Slow Shadow/Advisory, and the frozen baseline; apply the declared falsifiers and keep hypotheses optional if benefit is not demonstrated
- [ ] 10.4 Do not claim graduation until Spec → boundary → symbol → deterministic → stateful/integration → fresh production evidence → regression/Guard is complete and Human authorizes lifecycle advancement

## 11. R7 composition convergence and bounded Agent correction consumer

- [x] 11.1 Record `CURRENT_V2_FLOW_MAP` and `AGENT_OBLIGATION_OWNERSHIP_MAP`, including the zero-production-V2-path finding, correction break, visited/pending/completed meanings, and KEEP/MOVE/MERGE/DELETE/DEFER reconciliation
- [x] 11.2 Record the passed Human decisions `CONTAINER_RUNTIME_V2_COMPOSITION_CONVERGENCE_REQUIRED` and `CONTAINER_RUNTIME_V2_AGENT_CORRECTION_CONSUMER_APPROVED_BOUNDED` without expanding Goal/action/recovery/provider authority
- [x] 11.3 Add one stateless `ContainerRuntimeV2` lifecycle facade over the existing reducer/Graph/Fast/Slow/correction/checkpoint seams with exact shared evidence binding and immutable unified read output
- [x] 11.4 Add the single Agent-owned correction consumer using existing `_branchProgress` immutable replacement; keep/restore intended C pending, never complete observed D from correction, preserve unrelated progress, and make duplicate consumption idempotent
- [x] 11.5 Prove traversal and directed wrong-branch stateful scenarios, stale/wrong ref fail-closed behavior, historical correction without current-world rewrite, no action/recovery/Goal effect, and `NET_NEW_MUTABLE_TRUTH = 0`
- [x] 11.6 Add architecture guards for sole V2 composition, sole Agent correction owner, no direct Runtime progress writes, no current/trust/checkpoint/progress duplicate, no Graph/Fast/Slow action authority, and reasonable pure/component direct-call exceptions
- [x] 11.7 Run focused C1-C24 coverage inventory, solution build, Architecture Guards, relevant suites, consistency, and strict OpenSpec; classify rather than hide environmental/configuration failures

## 12. R8 bounded live physical-current state replacement

- [x] 12.1 Record `CONTAINER_RUNTIME_V2_LIVE_STATE_REPLACEMENT_APPROVED_BOUNDED` and produce `LIVE_STATE_REPLACEMENT_MAP` with old/new owner, write/read path, compatibility projection, mutable-truth status, tests, guards, and the atomic flip requirement
- [x] 12.2 Add pure Agent-private production lifecycle-input and compatibility projection helpers at the existing reconciliation ownership without adding a live V2 state slot or changing behavior
- [x] 12.3 Atomically introduce the sole Agent-owned immutable `ContainerRuntimeV2State`, remove the `_belief` mutable field, and route initial/fresh/recovery accepted observations through V2 state replacement with Fast live and Slow Disabled
- [x] 12.4 Derive `Agent.Belief`, legacy typed transition history, and existing ContainerContext reads one-way from the accepted V2 state/occurrence while preserving append-only audit history and `Observed != Execution`
- [x] 12.5 Preserve `ActiveContainerContext` as execution/completeness/path only; preserve Container local observation ownership, `_branchProgress`, GoalEvidence, action authorization, recovery, verified return, and external-boundary behavior without copied state
- [x] 12.6 Add L1-L18 deterministic/stateful coverage and architecture guards for sole physical-current ownership, exact belief/occurrence compatibility, r5 mismatch, multi-entry/path-relative return, working unknown/off-path nodes, Slow Disabled, and `NET_NEW_MUTABLE_TRUTH = 0`
- [x] 12.7 Migrate DriverHost/read projections to authority-free V2 current/entry/occurrence evidence with explicit compatibility and unavailable classifications; change no Driver authority or external protocol without a separate Gate
- [x] 12.8 Run focused Runtime V2/Agent/transition/return/recovery/DriverHost suites, Architecture Guards, solution build, relevant full suite, consistency, strict OpenSpec, and diff checks; classify environment/config failures and publish the R8 result

## Design Docs

> Auto-generated from proposal Impact section and refined for this repository.
> Implementation agents: read these before starting.

| Module | Design Doc |
|---|---|
| `src/UniClaw.Runtime/Agent/` | `openspec/changes/container-runtime-v2-core-semantics/design.md` + `docs/system/layers/agent-runtime.md` |
| `src/UniClaw.Runtime/Container/` | `openspec/changes/container-runtime-v2-core-semantics/design.md` + `docs/system/layers/container-runtime.md` |
| `src/UniClaw.Runtime/Model/` | `openspec/changes/container-runtime-v2-core-semantics/design.md` + `docs/system/patterns/state-and-belief-model.md` |
| `src/UniClaw.Runtime/Capabilities/Perception/Semantic/` | `openspec/changes/container-runtime-v2-core-semantics/specs/runtime-v2-semantic-assessment/spec.md` |
| `src/UniClaw.Runtime.DriverHost/` | `docs/system/patterns/observability-and-results.md` + this change's read-projection requirements |
| `tests/UniClaw.Runtime.Tests/` | `tests/UniClaw.Runtime.Tests/AGENTS.md` |
