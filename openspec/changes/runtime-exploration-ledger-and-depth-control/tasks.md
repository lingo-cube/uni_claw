## 1. Human Apply Gate

- [x] 1.1 Obtain explicit human approval for production apply of this proposal; freeze the approved design/spec revision before production edits.
  - Evidence (2026-08-24): human approved via UniFlow Option A; design/spec revision frozen at commit-time proposal state (design.md D1-D5, spec 6 requirements).
- [x] 1.2 Record implementation and independent-review owners (Luna / Sol role model).
  - Evidence (2026-08-24): DSH coding agent acts as Luna (implementation); Sol independent review occurs at graduation verification (tasks 7.x + separate graduation decision).

## 2. Ledger Projection Model

- [x] 2.1 Add immutable `ExplorationLedgerView` with per-scope discovered, visited, pending, unresolved, and unknown-frontier accounting, observation-sequence correlations, and a deterministic digest.
  - Evidence: `ExplorationLedgerView` + `ExplorationScopeLedger` (Model/ExplorationLedger.cs) with per-scope discovered/visited/pending/unresolved/unknown-frontier accounting, observation-sequence correlations, SHA-256 deterministic digest.
- [x] 2.2 Add a pure on-demand compiler from existing evidence records (branch progress, revisit coverage, structural-progress facts); no persistence, no new mutable state, no evidence mutation.
  - Evidence: pure static `ExplorationLedgerCompiler` (Model/ExplorationLedgerCompiler.cs) compiling from `BranchProgressEvidence` inputs; no persistence, no mutable state, no evidence mutation; ctor normalizes default arrays.
- [x] 2.3 Prove determinism: identical evidence produces identical ledgers and digests.
  - Evidence: `ExplorationLedgerTests.IdenticalEvidence_ProducesIdenticalLedgerAndDigest` + `DifferentEvidence_ProducesDifferentDigest`; record equality via structural `ImmutableArray` comparison (hand-written Equals).

## 3. Exploration Rule Vocabulary and Classification

- [x] 3.1 Add the closed `ExplorationRule` vocabulary (`ExpandContainer`, `RecordOnly`) derived at admission from the accepted strategy's exploration intent.
  - Evidence: closed `ExplorationRule` vocabulary (ExpandContainer/RecordOnly) + `DeriveRules` derived exclusively from accepted `ExplorationIntent`; no rule authoring surface.
- [x] 3.2 Classify discovered nodes at the existing generic semantic capability seam; unclassifiable nodes fail closed to unresolved ledger entries, never guessed.
  - Evidence (re-verified 2026-08-25 after WI-RELC-003 remediation): real-path fail-closed at the CP-12 seam (`Agent.OpenWorld.cs`) — a CONFIGURED classifier returning null records the node via Agent-owned `_unresolvedNodes` evidence (distinct-identity sets, mirroring `_unknownFrontierBeyondDepth`) with a Trace disposition, no authorization, no dispatch, no Tap fallback; the unconfigured-classifier legacy path is unchanged. Real-path test `UnresolvedNodeFailClosedPathTests` (Fake World): zero Tap of the node, never in Authorized/CompletedSiblingEvidence, ledger Root scope `Unresolved >= 1` and `Visited` excludes it. Ledger unresolved counts are now evidence-derived, not hardcoded.
  - History: completion claim revoked 2026-08-25 by Leader verification (probe proved the node was guessed into dispatch and counted visited); remediated by WI-RELC-003 (validated WorkItem, development profile, single worker) and independently re-verified.
- [x] 3.3 Record `Visited` only on rule-satisfaction evidence (fresh-observation record for record-only; verified subtree return or boundary disposition for containers); dispatch alone never marks visited.
  - Evidence (ledger level): visited counted only from completed-sibling evidence (verified completion/return); authorization/dispatch alone counts pending (`Visited_RequiresCompletionEvidence_NotAuthorizationOrClick`, `CompileScope_ReportsUnifiedAccountingFromEvidence`). In-loop wiring of fresh-observation record-only visits lands with the 4.2/5.1 integration increment.
  - Verified complete (2026-08-25, Leader): record-only fresh-observation visited is wired on the real path (`Depth0_RecordOnlyNode_VisitedByObservation_WithZeroDispatch` — zero dispatch, visited=2); container visited-by-verified-return is code-verified at all three write sites (`WithCompletedSibling` only at the verified parent-return seam, leaf post-action completion, and `WithVerifiedBoundaryDisposition`); the 3.2 fail-closed gap that previously let an unclassifiable node be guessed into dispatch-and-visited was remediated by WI-RELC-003.

## 4. Semantic Depth Control

- [x] 4.1 Map declared maximum depth to admission-validated semantics (0 root record-only; 1 root + direct children; N bounded recursive).
  - Evidence: `DeriveDepthSemantics` admission mapping (0=RootRecordOnly, 1=RootAndDirectChildren, N>=2=BoundedRecursive); tested incl. MaximumSupportedDepth boundary.
- [x] 4.2 Implement bounded-record boundary behavior: containers at the declared boundary processed record-only with unknown-frontier ledger entries; preserve the existing fail-closed cutoff for exhaustive strategies unchanged.
  - Evidence: `Agent.OpenWorld.cs` cutoff path — depth>=2 BoundedRecursive keeps fail-closed cutoff verbatim (frozen); depth 0/1 bounded-record processes boundary nodes record-only with per-page unknown-frontier ledger entries (`_unknownFrontierBeyondDepth`); root boundary via fresh GoalEvidence, child boundary via verified parent return. Tests `ExplorationDepthBoundaryTests` 4/4 (depth0 record-not-fail + frontier, depth0 unsatisfied-goal boundary fail, depth1 verified return, depth2 cutoff verbatim). Orchestrator independently verified: build 0 errors, 25/25 targeted, 1996/1996 + 32/32 full deterministic.
- [x] 4.3 Prove depth immutability for an active Run.
  - Evidence: maximumDepth is a method parameter with no mutation path; `Depth2_Exhaustive_CutoffStillFailsClosed` + code inspection confirm depth immutability for an active Run.

## 5. Exposure and Authority Guards

- [x] 5.1 Expose the ledger as an Agent-readable evidence projection on existing snapshot/evidence surfaces; no new wire methods, no GoalEvidence mutation, no FSM interaction.
  - Evidence (re-verified 2026-08-25 after WI-RELC-003 remediation): `Agent.CompileExplorationLedgerView` compiles from real per-scope evidence — `_branchProgress` + `_unresolvedNodes` (real unresolved counts) + `_unknownFrontierBeyondDepth` + `_revisitCoverage` (fail-closed consistency cross-check input). Read-only, stateless, no persistence, Idle fail-closed, no wire methods, no GoalEvidence/FSM touch.
  - History: completion claim revoked 2026-08-25 (hardcoded `Unresolved: 0` made the unresolved accounting non-evidence-derived); remediated by WI-RELC-003 and independently re-verified.
- [x] 5.2 Add reflection and dependency guards: ledger/rule types carry no action, authorization, transition, completion, or recovery members and depend only on existing evidence record types.
  - Evidence: `tests/UniClaw.Runtime.Tests/Architecture/ExplorationLedgerAuthorityGuardTests.cs` — 5 guards green (authority-member reflection incl. generic unwrapping, type-reference bans, compiler closed allowlist, pure-static surface); independently re-verified 5/5 by orchestrator.
- [x] 5.3 Extend the scenario-neutrality guard to classification and ledger sources.
  - Evidence: `LedgerSources_AreScenarioNeutral` — CodeOnly comment-stripped source scan of both Model files; zero scenario tokens; no whitelist.

## 6. Deterministic Tests

- [x] 6.1 Test unified accounting across branch-progress, coverage, and structural-progress evidence using generic Fake World bindings.
  - Evidence: `CompleteLedger_PendingZeroFrontierZero_*` tests consume `Agent.CompileExplorationLedgerView` end-to-end over a generic Fake World run — real branch-progress evidence flows through the unified ledger compiler with verified per-scope accounting (Discovered=2/Visited=2 at Root, pending/frontier assertions across all scopes).
- [x] 6.2 Test click-without-evidence is not visited; record-only visited by observation; container visited by verified subtree return.
  - Evidence: `Visited_RequiresCompletionEvidence_NotAuthorizationOrClick` (click/dispatch ≠ visited), `CompileScope_ReportsUnifiedAccountingFromEvidence` (record/complete path), `UnclassifiableNode_FailsClosedToUnresolved_NeverGuessed`.
- [x] 6.3 Test bounded-record boundary vs exhaustive fail-closed cutoff divergence.
  - Evidence: `Depth0_BoundedRecord_ContainersRecordedNotFailed_FrontierLedgered` (bounded-record side) vs `Depth2_Exhaustive_CutoffStillFailsClosed` (fail-closed side) prove the divergence; `HypothesisReconcilerTests.cs:150` cutoff-message assertion unchanged and passing.
- [x] 6.4 Test unclassifiable node → unresolved, no inferred rule.
  - Evidence (re-verified 2026-08-25 after WI-RELC-003 remediation): real-path test `UnresolvedNodeFailClosedPathTests` (Fake World run through `IntentExecution.RunOpenWorldAsync`): the unclassifiable branch is never dispatched (bounds-targeted Tap assertion), never authorized/completed, Trace records the unresolved disposition, and `CompileExplorationLedgerView` reports `Unresolved >= 1` with `Visited` excluding the node. Compiler-level accounting additionally covered by `UnclassifiableNode_FailsClosedToUnresolved_NeverGuessed`.
  - History: completion claim revoked 2026-08-25 (the test only hand-fed `unresolvedCount: 1` into the compiler — arithmetic, not the real path); remediated by WI-RELC-003 with a real-path test and independently re-verified.
- [x] 6.5 Test satisfied ledger (pending = 0, frontier = 0) does not complete a Run without Agent GoalEvidence and FSM authorization.
  - Evidence: `CompleteLedger_PendingZeroFrontierZero_GoalSatisfied_CompletesViaGoalEvidence` + `..._GoalUnsatisfied_RunStillFails` — complete ledger (pending=0, frontier=0) with unsatisfied GoalEvidence still Fails; completion authority remains Agent GoalEvidence + FSM.
- [x] 6.6 Test depth mutation is rejected for an active Run.
  - Evidence: `DepthIsRunImmutable_AgentPublicSurface_ExposesNoDepthMutationPath` — reflection: no setters, no settable depth property, no by-ref parameters; maximumDepth pass-by-value only.

## 7. Regression and Validation

- [x] 7.1 Run Strategy Contract, strategy execution loop, Phase 1–4, OpenWorld, DFS, lifecycle, recovery, verification, GoalEvidence, FSM, and Traversal regression suites.
  - Evidence (2026-08-25): Strategy Contract, StrategyExecutionLoop, Phase 1-4, OpenWorld, DFS, lifecycle, recovery, verification, GoalEvidence, FSM, Traversal regressions all green in full deterministic run.
- [x] 7.2 Run full deterministic solution tests, architecture guards, `scripts/check-consistency.sh`, `git diff --check`, and strict OpenSpec validation.
  - Evidence (2026-08-25): build 0 warnings/0 errors; deterministic full suite 1999/1999 Runtime + 32/32 Semantic green (7 RealDevice/RealEmulator tests fail-closed on absent ADB device — hardware availability, by design, unchanged); `scripts/check-consistency.sh` C1-C12 ALL PASS; `git diff --check` PASS; `openspec validate runtime-exploration-ledger-and-depth-control --strict` PASS. All independently re-verified by orchestrator.
- [x] 7.3 Report device-dependent test limitations honestly; do not claim graduation from task completion.
  - Evidence: device-dependent limitations reported honestly above; graduation NOT claimed from task completion — graduation requires separate human-authorized verification per lifecycle rules.

## Design Docs

> Implementation agents must read these artifacts before starting.

| Area | Design Doc |
|---|---|
| Change scope and rationale | `proposal.md` |
| Projection/rule/depth design decisions | `design.md` |
| Normative behavior | `specs/runtime-exploration-ledger-and-depth-control/spec.md` |
| Roadmap context | `docs/decisions/runtime-exploration-roadmap.md` (Phase 2) |

## 8. Graduation Verification (2026-08-25)

- Sol independent adversarial verification returned **GRADUATION: NOT_EARNED**.
- Blocking finding: spec Requirement 3 scenario "Record-only node visited by observation"
  has no production wiring (boundary record-only nodes count only into
  `_unknownFrontierBeyondDepth`, never into visited) and no asserting test; tasks 3.3/6.2
  evidence overstated coverage.
- Non-blocking recorded limitations: (a) Unresolved channel production-unreachable
  (fail-closed classification occurs at capability seam; ledger always reports 0 —
  honestly documented); (b) rule vocabulary is provenance not admission mechanism;
  (c) ledger inputs narrower than Req-1 preamble (no revisit-coverage/structural-fact inputs);
  (d) depth semantics derived at boundary not admission (equivalent by value-immutability).
- Remediation in progress: boundary record-only visited wiring (within frozen design D3 scope).

### 8.1 Remediation and Leader re-verification (2026-08-25, later session)

- The section-8 blocking finding is REMEDIATED: the R3/R4 overlap is implemented
  (boundary record-only nodes count as Visited via the fresh-observation record AND as
  unknown frontier — overlapping annotations), with real-path tests
  `Depth0_BoundedRecord_ContainersRecordedNotFailed_FrontierLedgered` and
  `Depth0_RecordOnlyNode_VisitedByObservation_WithZeroDispatch`.
- Leader independent verification found TWO further spec gaps not covered by section 8:
  1. **Requirement 2 violated on the real Agent path** — unclassifiable pending nodes are
     guessed into dispatch (`Agent.OpenWorld.cs:543-550` + `default: Tap` at `:806`),
     authorized into branch-progress evidence, and counted Visited;
     `CompileExplorationLedgerView` hardcodes `Unresolved: 0`. Sol's limitation (a) was
     mis-classified as non-blocking. Tasks 3.2 / 5.1 / 6.4 revoked above.
  2. **Requirement 1 source fusion incomplete** — revisit-coverage records are not a
     compiler input (limitation (c), confirmed). Structural-progress facts carry no
     per-scope content (sole producer emits one synthetic Run-level `BoundedScopeEntered`
     fact with an opaque reference, `Agent.PreTerminalCycle.cs:41`); honest fusion would
     require new evidence semantics — reserved for the Human Gate, not self-extended.
- Remediation WorkItem(s) dispatched per UniFlow (validated JSON WorkItem, matching
  profile, single worker owner); see `docs/work/active/` records.

### 8.2 WI-RELC-003 remediation accepted (2026-08-25, later session)

- WorkItem `WI-RELC-003` (development / runtime-core, validated
  `WORK_ITEM_VALIDATION_PASS`, single unicast worker) remediated both gaps:
  real-path fail-closed unclassifiable nodes (Agent-owned `_unresolvedNodes`
  distinct-identity evidence + CP-12 seam guard before authorization; legacy
  unconfigured-classifier path unchanged) and revisit-coverage as a fail-closed
  compiler cross-check input (five counts unchanged by construction).
- Leader acceptance was INDEPENDENT: scope-write containment checked (spec.md /
  design.md mtimes predate dispatch), implementation re-read line-by-line, and
  every gate command re-executed by the Leader (not trusting worker self-report):
  build 0 errors; targeted 72/72; full deterministic 2004/2004 Runtime +
  32/32 Semantic excluding RealDevice/RealEmulator/RealityBaseline; the only
  failures are the 7 pre-existing environmental real-device tests (no ADB);
  `openspec validate --strict` PASS; `scripts/check-consistency.sh` ALL PASS;
  `git diff --check` PASS.
- Tasks 3.2 / 3.3 / 5.1 / 6.4 re-checked above with remediated evidence.
- Documented interpretation boundary (surfaced for the Human Gate, not
  self-extended): Requirement 1's evidence-family enumeration includes
  "structural-progress facts", but the only existing producer emits a single
  Run-level synthetic revision marker with no per-scope node content — there is
  no node-accounting content to fuse, and inventing per-scope structural-fact
  semantics would be a new evidence design requiring authorization. The Req-1
  normative scenario (branch-progress + coverage → per-scope counts) is
  satisfied; observation-sequence correlation is preserved per scope
  (`SourceObservationSequence`).
