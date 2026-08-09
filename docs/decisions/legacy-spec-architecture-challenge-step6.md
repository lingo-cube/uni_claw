# LEGACY_SPEC_ARCHITECTURE_CHALLENGE_RESULT — Step 6

> Generated: 2026-08-09
> Primary input: `docs/decisions/legacy-scenario-pressure-step5.md`
> Supporting evidence chain: Steps 1–5
> Current system: `uni-agent` branch (S0_GRADUATED, 2026-08-09)

---

## Challenge Summary

**Scenario Pressures Reviewed:** 13

| Classification | Count |
|---|---|
| FROZEN_CAPABILITY_COVERED | 10 |
| COVERED_BUT_REPLAY_EVIDENCE_NEEDED | 1 |
| SPECIFICATION_GAP | 1 |
| EXPLICITLY_DEFERRED_CAPABILITY | 1 |
| COMPOSITION_PROOF_GAP | 0 |
| IMPLEMENTATION_BEHAVIOR_GAP | 0 |
| SEMANTIC_MODEL_GAP | 0 |
| ARCHITECTURE_PRESSURE | 0 |
| INSUFFICIENT_CURRENT_OR_LEGACY_EVIDENCE | 0 |

**Result:** The current Runtime passes 10 of 13 reality-derived Scenario Pressures with frozen capability coverage. One scenario (SP-05) needs S1 replay evidence to strengthen its real-device provenance. One scenario (SP-11) has a specification gap — the semantic model CAN express it but no normative spec or test currently requires it. One scenario (SP-09) belongs to an explicitly deferred capability (Intent→Goal/Plan synthesis). Zero scenarios reveal a semantic model gap or architecture pressure.

---

## Per-Scenario Challenge

---

### SP-01 — Entry Action Must Verify World Effect

**Title:** App entry action must verify foreground state before traversal begins

**Primary RD:** RD-01 (ActionExecution != ActionEffect)

---

**1. REALITY REQUIREMENT**

Before beginning traversal, the system must obtain fresh observational evidence that the intended application is in the foreground and that the screen contains content consistent with the expected entry point. If this evidence cannot be obtained, the system must report entry failure — not proceed to traversal on an unverified screen.

---

**2. CURRENT NORMATIVE REPRESENTATION**

- `Startup.Startup.cs` §19 sequence step 4: `ForegroundApplication == targetApplicationIdentity` comparison. Failure → `StartupResult.NotReady(reason)` with explicit reason including observation sequence number.
- `run-lifecycle/spec.md`: Startup sequence Attach → Launch → Observe → Verify ForegroundApplication → Resolve → Establish Container → Establish RecoveryAnchor → Ready. Failure → `Failed`, no recovery actions in Phase 1.
- Architecture invariant I-4: Observation is evidence, not semantic truth.
- Architecture invariant I-9: Recovery requires observe→verify→reconcile.
- `RecoveryAnchor`: carries ApplicationIdentity + ExpectedSemanticEntry — the trusted re-entry point established at Startup.

---

**3. CURRENT SPEC COVERAGE:** FULL

Evidence: `run-lifecycle/spec.md` SHALL statements requiring ForegroundApplication verification before Ready state.

---

**4. PRODUCTION BEHAVIOR COVERAGE:** FULL

Evidence: `Startup.cs` line implementing `ForegroundApplication == targetApplicationIdentity` comparison. `NotReady` with explicit reason string on mismatch. Agent.RunAsync only proceeds past Startup on `Ready(anchor)`.

---

**5. EXECUTABLE PROOF:** FULL

- `StartupForegroundVerificationFailureTests.cs`: assertions 1-6 — never Running, NotReady reason in Trace, no anchor, Failed final, only `[LaunchApp]` action dispatched, no Container/Step/evidence.
- `Unit/StartupTests.cs`: `StartupForegroundFailVariant_Startup_NotReadyExplicitReason_NoAnchor_NoFurtherActions`, `NoFurtherCallsAfterVerificationFailure`, `SemanticResolutionFailure_NotReadyWithExplicitReason_NoAnchor`.
- `CapstoneSettingsIntegrationRunTests.cs`: Startup succeeds → Ready → Container bind → traversal proceeds.

---

**6. LEGACY EVIDENCE MATURITY VALUE:** NO

The legacy E-13-A gap (EntryPolicy fake success) documents a failure mode in the old system. The current Startup verification requirement already prevents this failure class in synthetic S0. Real-device entry verification (Phase 4) will be the meaningful challenge.

---

**7. OWNERSHIP / AUTHORITY FIT:** YES

Current owner: `Startup.Startup` (Startup execution state). Decision authority: `Agent` (proceeds only on Ready). No ownership change needed.

---

**8. PRIMARY CLASSIFICATION:** FROZEN_CAPABILITY_COVERED

---

**9. WHY**

- Startup sequence §19 normatively requires foreground verification before Ready
- `StartupResult.NotReady` with explicit reason is the failure path
- Proven by dedicated scenario test (6 assertions) + unit tests (3 variants)
- Architecture invariants I-4/I-9 reinforce the semantic boundary
- Legacy E-13-A gap (fake success) is prevented by the current requirement

---

**10. NEXT TREATMENT:** NONE

---

### SP-02 — Navigation Action Must Verify Page Change

**Title:** Navigation action must detect when the observed world did not change

**Primary RD:** RD-01 (ActionExecution != ActionEffect)

---

**1. REALITY REQUIREMENT**

After tapping an element intended for navigation, the system must compare pre-action and post-action observations. If materially identical (no page change detected), the system must NOT treat the action as successful navigation and must not repeatedly tap the same element.

---

**2. CURRENT NORMATIVE REPRESENTATION**

- `Traversal.Traversal.cs`: step pipeline Select→Check→Execute→Observe→Verify. Post-action Observe is mandatory. Verify phase checks Container continuity.
- `Container.Container.cs`: `IsStillMine(Observation)` — determines whether the observation still belongs to this Container. `IsLocalObstructionHypothesis` detects Unknown page + same foreground + !IsStillMine.
- `ActionResult`: `Dispatched / TimedOut / Rejected` — dispatch outcome never proves world change (裁决 10).
- Architecture invariant I-4: Observation is evidence, not truth.
- Architecture invariant I-5: Plan is hypothesis, not reality.
- SC-P3-001 (Uncertain Action): TimedOut transport ≠ world result; must re-Observe before verdict.

---

**3. CURRENT SPEC COVERAGE:** FULL

Evidence: `container-traversal/spec.md` SHALL requiring post-action Observe. `uncertain-action` spec requiring re-Observation on TimedOut. `ActionResult` spec prohibiting dispatch outcome as world proof.

---

**4. PRODUCTION BEHAVIOR COVERAGE:** FULL

Evidence: Traversal step pipeline executes Observe after every Execute. Container.IsStillMine comparison on post-action observation. Agent detects mismatch → obstruction hypothesis or Container rebind.

---

**5. EXECUTABLE PROOF:** FULL

- `UncertainActionVerificationTests.cs`: TimedOut dispatch → world observed → evaluator decides applied→Completed / absent→Failed. No redispatch.
- `PopupObstructionRecoveryTests.cs`: page-changed branch (rebind), original progress preserved.
- `ViewportIdentityContinuityTests.cs`: fresh evidence required, stale evidence → escalation Trap.
- `CapstoneSettingsIntegrationRunTests.cs`: all 14 pages navigated with verified transitions.

---

**6. LEGACY EVIDENCE MATURITY VALUE:** NO

Legacy E-09-L4 (stale click) and E-09-L8 (subtitle double-click) document real-run failures. Current S0 semantics prevent these failure classes in the synthetic model. Real-device stale-click detection (Phase 4) will be the meaningful challenge.

---

**7. OWNERSHIP / AUTHORITY FIT:** YES

Current owner: `Traversal` (step execution), `Container` (page-local continuity). Decision authority: `Agent` (proceeds or escalates). No ownership change needed.

---

**8. PRIMARY CLASSIFICATION:** FROZEN_CAPABILITY_COVERED

---

**9. WHY**

- Traversal Observe→Verify cycle normatively requires post-action observation
- Container.IsStillMine + ActionResult model prevent dispatch-as-proof conflation
- SC-P3-001 Uncertain Action explicitly requires re-Observation on TimedOut
- Proven by uncertain-action, popup-obstruction, and viewport-continuity scenario tests
- Architecture invariants I-4/I-5 reinforce the semantic boundary

---

**10. NEXT TREATMENT:** NONE

---

### SP-03 — Multi-Branch Hub Must Not Report Complete With Unvisited Branch

**Title:** A hub with multiple navigation branches must not report AllVisited when a branch remains entirely unexplored

**Primary RD:** RD-02 (WorkDispatched != WorkCompleted)

---

**1. REALITY REQUIREMENT**

When a hub page has multiple observable navigation targets, the system must dispatch work on ALL targets before reporting completion. Completion must not be claimed while any observable navigation target remains undispatched. The legacy E-07 bug (hub→listA traversed, listB 0/16, AllVisited reported) must be impossible.

---

**2. CURRENT NORMATIVE REPRESENTATION**

- `BranchProgressEvidence`: immutable snapshot with ApprovedSiblingEvidence (all known navigation targets) and CompletedSiblingEvidence (all targets proven exhausted). Invariant: completed ⊆ approved. `IsSubtreeComplete` derived only when all approved siblings have completion evidence.
- SC-P3-CAND-004 (Sibling Branch Progress): child→parent→sibling progress tracking. Completion requires ALL approved siblings proven.
- SC-P3-CAND-008 (Bounded Cross-Page Discovery): BranchInventoryEvidence carries RequiredBranchEvidence map — complete bounded inventory.
- `GoalEvidence.Satisfied`: Agent completion exclusively through Goal evidence (I-10). Plan exhaustion alone never completes.
- `Capstone`: 14 approved pages, all must be complete before GoalEvidence.Satisfied.

---

**3. CURRENT SPEC COVERAGE:** FULL

Evidence: CAND-004 spec requiring IsSubtreeComplete only when all approved siblings have completion evidence. CAND-008 spec requiring BranchInventoryEvidence for discovered branches. GoalEvidence spec prohibiting completion from plan exhaustion.

---

**4. PRODUCTION BEHAVIOR COVERAGE:** FULL

Evidence: Agent.RunAsync records BranchProgressEvidence only when child is locally complete + fresh parent reconcile. GoalEvidence evaluator (caller-injected) reads BranchProgress to determine satisfaction. Agent checks GoalEvidence.Satisfied — if any approved sibling is missing completion evidence, the evaluator returns unsatisfied.

---

**5. EXECUTABLE PROOF:** FULL

- `SiblingBranchProgressScenarioTests.cs`: AOnly (B pending → cannot fabricate → Failed), EarlyReturn (no completion recorded), RevisitA_IsIdempotent, WrongParent rejected.
- `CapstoneSettingsIntegrationRunTests.cs`: completed only when all 14 approved pages have progress evidence (seq 36). Progress snapshots monotone. No double-counting (33 distinct journal StepIds).
- `CapstoneSettingsFormalProofTests.cs`: Assertion 2/4 — all approved branches complete. Assertion 3 — no approved branch unresolved.

---

**6. LEGACY EVIDENCE MATURITY VALUE:** YES

Legacy E-07 (MultiBranchNavigation — unfixed bug) is the strongest false-completion evidence in the corpus. The legacy test explicitly demonstrates the bug: hub→listA 16/16, listB 0/16, AllVisited reported. Attaching this as S1 replay evidence would challenge the BranchProgressEvidence model against a concrete recorded failure, strengthening provenance from "synthetic model prevents it" to "recorded reality confirms the prevention."

---

**7. OWNERSHIP / AUTHORITY FIT:** YES

Current owner: `Agent` (BranchProgressEvidence snapshots), `Container` (local completion). Decision authority: `Agent` (GoalEvidence.Satisfied). No ownership change needed.

---

**8. PRIMARY CLASSIFICATION:** FROZEN_CAPABILITY_COVERED

---

**9. WHY**

- BranchProgressEvidence.IsSubtreeComplete requires all approved siblings
- GoalEvidence.Satisfied is the exclusive completion gate (I-10)
- Capstone proves 14-page multi-branch completion without false positives
- Legacy E-07 bug class is structurally prevented by the current model
- S1 replay of E-07 would strengthen provenance but is not required for semantic coverage

---

**10. NEXT TREATMENT:** ATTACH_LEGACY_EVIDENCE (S1 replay of E-07)

---

### SP-04 — Declared Depth Bound Must Be Enforced During Discovery

**Title:** A declared depth constraint must prevent entry into deeper discoverable pages

**Primary RD:** RD-03 (ConstraintDeclared != ConstraintEnforced)

---

**1. REALITY REQUIREMENT**

When a task declares maxDepth = N, the system must not navigate to depth N+1 even when navigable elements are observable at depth N. The constraint must be enforced at every execution point where new navigation targets are discovered, not just at plan construction time.

---

**2. CURRENT NORMATIVE REPRESENTATION**

- SC-P3-CAND-008 (Bounded Cross-Page Discovery): `BranchInventoryEvidence` carries `RequiredBranchEvidence` map. `semanticDepth` parameter tracks current depth against `DepthBound`. Empty map = positive leaf at this depth. Null = unresolved. Discovery respects depth bound.
- `Capstone`: DepthBound=4. All 14 approved pages within depth ≤ 4. CapstoneSettingsWorldFixture enforces DepthBound ≥ 4.
- Architecture invariant I-8: lower scope escalates but never steals higher-scope authority. Depth constraint is an Agent-level boundary that Container-level discovery must respect.

---

**3. CURRENT SPEC COVERAGE:** FULL

Evidence: CAND-008 spec requires BranchInventoryEvidence to respect depth bound. Discovery at depth ≥ bound produces empty-map (leaf) or null (unresolved), never generates navigation tasks beyond bound.

---

**4. PRODUCTION BEHAVIOR COVERAGE:** FULL

Evidence: Agent.RunAsync passes semanticDepth to branch inventory evaluation. BranchInventoryEvidence with empty map at depth bound indicates "nothing more to discover at this depth."

---

**5. EXECUTABLE PROOF:** FULL

- `BoundedCrossPageDiscoveryScenarioTests.cs`: complete:depth=0, complete:depth=1, leaf:depth=2. Depth-bound enforcement proven.
- `CapstoneSettingsIntegrationRunTests.cs`: all 14 approved pages within DepthBound=4. No depth-5 page visited.
- `CapstoneSettingsFormalProofTests.cs`: Assertion 2/4 — depth bound respected.

---

**6. LEGACY EVIDENCE MATURITY VALUE:** YES

Legacy E-11 (SettingsEnumerateRegression) and E-08 Step2 (depth=4 reproduced from real run) provide strong recorded-reality evidence of depth constraint violation. Attaching as S1 replay would challenge the current depth-bound enforcement against a concrete historical failure where DynamicMatch sub-frame generation ignored maxDepth.

---

**7. OWNERSHIP / AUTHORITY FIT:** YES

Current owner: `Agent` (depth bound, branch inventory evaluation). Decision authority: `Agent` (proceeds or escalates). No ownership change needed.

---

**8. PRIMARY CLASSIFICATION:** FROZEN_CAPABILITY_COVERED

---

**9. WHY**

- CAND-008 BranchInventoryEvidence explicitly enforces depth bound during discovery
- Proven by dedicated depth-0/1/2 scenario tests + leaf-boundary test
- Capstone proves 14-page traversal within DepthBound=4
- Legacy E-11/E-08 depth-runaway evidence is semantically covered
- S1 replay would strengthen provenance from synthetic to recorded-reality

---

**10. NEXT TREATMENT:** ATTACH_LEGACY_EVIDENCE (S1 replay of E-11 + E-08)

---

### SP-05 — Observation Failure Must Not Become Content Exhaustion

**Title:** A failed scroll query must not be treated as proof that the end of the list has been reached

**Primary RD:** RD-04 (ObservationFailed != ContentExhausted)

---

**1. REALITY REQUIREMENT**

When a device query for scroll state fails (timeout, error, incomplete data), the system must distinguish "query failed, scroll state unknown" from "end of list confirmed." The system must NOT report IsEnd=true based on a failed query. The unknown state must be diagnosable.

---

**2. CURRENT NORMATIVE REPRESENTATION**

- `ViewportExplorationEvidence`: three states — `true` (continue, more content exists), `false` (positive exhaustion, end confirmed), `null` (unresolved — cannot determine). Explicit tri-state semantics.
- SC-P3-CAND-007 (Viewport Exploration): `true` authorizes at most one ScrollForward. `false` = positive exhaustion proven. `null` = honestly unresolved.
- Architecture invariant I-4: Observation is evidence, not truth.
- The tri-state model cleanly separates "confirmed exhausted" from "unresolved/unknown."

---

**3. CURRENT SPEC COVERAGE:** PARTIAL

Evidence: CAND-007 spec defines the three states and requires `null` → unresolved (no dispatch, no completion). However, the spec was designed for S0 synthetic environments where "observation failure" doesn't occur naturally. The spec handles the semantic distinction but does not explicitly SHALL-require that a device-query-failure produces `null` rather than `false`.

---

**4. PRODUCTION BEHAVIOR COVERAGE:** PARTIAL

Evidence: Current S0 implementation uses deterministic synthetic environments. The ViewportExplorationEvidence evaluator (caller-injected) can return `null` for any reason. But no production code path currently distinguishes "device query failed" from "content genuinely unknown" — both produce `null`. The distinction exists in the model but is not exercised at the Environment boundary. Real-device IEnvironment implementation is Phase 4 deferred.

---

**5. EXECUTABLE PROOF:** PARTIAL

- `ViewportExplorationScenarioTests.cs`: ambiguous branch (identical evidence → unresolved → Failed, no redispatch). Proves that `null` ≠ `false`.
- However, no test currently exercises "Environment query throws / returns error → ViewportExplorationEvidence = null." The S0 synthetic Environment never fails.

---

**6. LEGACY EVIDENCE MATURITY VALUE:** YES — STRONG

Legacy E-13-B (ADB scroll failure → IsEnd=true) is the canonical evidence. The legacy system explicitly conflated query failure with end-of-list. This is a documented P0-severity gap in production code (AdbScreenStateProvider.cs:38). S1 replay of this failure mode would directly challenge the current tri-state model: does the system actually produce `null` (not `false`) when the Environment fails?

---

**7. OWNERSHIP / AUTHORITY FIT:** YES

Current owner: `Container` (ViewportExplorationEvidence), caller-injected evaluator. Decision authority: `Agent` (interprets evidence). No ownership change needed. Real-device Environment failure is Phase 4 — the model accommodates it.

---

**8. PRIMARY CLASSIFICATION:** COVERED_BUT_REPLAY_EVIDENCE_NEEDED

---

**9. WHY**

- ViewportExplorationEvidence tri-state model (true/false/null) is semantically correct
- null ≠ false distinction is proven in S0 synthetic tests
- But no test exercises Environment-failure → null at the IEnvironment boundary
- Real-device observation failure is Phase 4 deferred — cannot be proven in S0
- Legacy E-13-B is the strongest recorded evidence of this specific conflation
- S1 replay would bridge the gap: proves the tri-state model holds under real failure conditions

---

**10. NEXT TREATMENT:** S1_REPLAY (E-13-B ADB scroll failure → IsEnd=true)

---

### SP-06 — Unchanging Content Must Not Loop Forever

**Title:** When scrolling produces no new observable content, the system must terminate without exhausting its step budget

**Primary RD:** RD-04 (ObservationFailed != ContentExhausted)

---

**1. REALITY REQUIREMENT**

When scrolling repeatedly produces identical content (no new items appear, scroll mechanism reports "scrollable" but content is static), the system must detect that scrolling is not revealing new content and terminate without exhausting its step budget.

---

**2. CURRENT NORMATIVE REPRESENTATION**

- `ViewportExplorationEvidence`: `false` = positive exhaustion (end of list confirmed). `true` authorizes at most one ScrollForward. Bound consumption without positive exhaustion → Failed with "semantic exhaustion 未获证明."
- SC-P3-CAND-007: viewport exploration bound prevents infinite loops. After bound is consumed without positive exhaustion, the system fails honestly rather than looping forever.
- Architecture invariant I-10: Completion requires Goal Evidence.

---

**3. CURRENT SPEC COVERAGE:** FULL

Evidence: CAND-007 spec requires bound-reached without positive exhaustion → Failed. ScrollForward bounded to at most one per exploration decision.

---

**4. PRODUCTION BEHAVIOR COVERAGE:** FULL

Evidence: ViewportExplorationEvidence evaluator returns true/false/null. Agent counts exploration attempts against bound. Bound exhaustion without positive false → Run Failed.

---

**5. EXECUTABLE PROOF:** FULL

- `ViewportExplorationScenarioTests.cs`: positive branch (true,true,false → exhausted → completion), ambiguous branch (identical → unresolved → Failed), bound-reached (Failed with "bound reached" + "semantic exhaustion 未获证明").
- `CapstoneSettingsIntegrationRunTests.cs`: exactly one scroll with honest exhaustion (seq 35→36).

---

**6. LEGACY EVIDENCE MATURITY VALUE:** YES

Legacy E-12-A (scroll-only dead-end — old behavior: infinite scroll until MaxSteps; new behavior: content stability K=3 → AllVisited) provides strong regression evidence. The current system uses a different mechanism (bound + positive exhaustion) rather than content-stability detection (K=3 same fingerprints). Attaching as S1 replay would compare the two approaches and verify the current mechanism doesn't reintroduce the old failure.

---

**7. OWNERSHIP / AUTHORITY FIT:** YES

Current owner: `Container` (ViewportExplorationEvidence), `Agent` (exploration bound). Decision authority: `Agent` (GoalEvidence.Satisfied). No ownership change needed.

---

**8. PRIMARY CLASSIFICATION:** FROZEN_CAPABILITY_COVERED

---

**9. WHY**

- Viewport exploration bound prevents infinite scroll loops
- Bound exhaustion without positive exhaustion → honest Failed (not false AllVisited)
- Proven by three ViewportExplorationScenarioTests variants
- Legacy E-12-A failure mode (infinite scroll → MaxSteps) is prevented
- Current mechanism (bound) differs from legacy (K=3 stability) but outcome is the same: no infinite loop

---

**10. NEXT TREATMENT:** ATTACH_LEGACY_EVIDENCE (S1 replay of E-12-A for mechanism-comparison evidence)

---

### SP-07 — Element Visibility Must Not Imply Navigability

**Title:** An element visible on screen must not be treated as a navigation target without evidence that tapping it produces navigation

**Primary RD:** RD-05 (ElementPresence != ElementNavigability)

---

**1. REALITY REQUIREMENT**

The system must use element type, text content, and spatial evidence — not mere presence — to decide navigability. Non-navigable elements (search inputs, subtitles, empty-text items) must not generate navigation tasks.

---

**2. CURRENT NORMATIVE REPRESENTATION**

- `CandidateAuthorizationEvidence`: pre-dispatch authorization check. `Authorized` (bool?): true = safe to dispatch, false = explicitly denied (destructive/state-changing), null = unresolved (cannot determine). Denied/unresolved candidates → zero dispatch with explicit Trace evidence.
- SC-P3-CAND-006 (Bounded Candidate Safety): destructive/state-changing/disapproved candidates → zero dispatch.
- SC-P1-005 (Same-Text Disambiguation): SetSwitch prioritizes switch-state-bearing elements.
- `Traversal.Select`: matches candidates by Text. Non-matching or missing candidates → Failed with zero dispatch.

---

**3. CURRENT SPEC COVERAGE:** FULL

Evidence: CAND-006 spec requires pre-dispatch authorization. Unauthorized candidates must not be dispatched. SC-P1-005 spec requires state-bearing element preference.

---

**4. PRODUCTION BEHAVIOR COVERAGE:** FULL

Evidence: Agent checks CandidateAuthorizationEvidence before dispatch. Authorized=false → zero dispatch with denial Trace event. Authorized=null → unresolved, no dispatch.

---

**5. EXECUTABLE PROOF:** FULL

- `BoundedCandidateSafetyScenarioTests.cs`: safe candidate dispatched; destructive/state-changing/unresolved → zero-dispatch with denial Trace events (no StepId/ActionId/Action).
- `SameTextElementDisambiguationTests.cs`: SetSwitch on non-switch candidate → Environment rejects → Step fails.
- `EscalationWithoutStealingAuthorityTests.cs`: missing target → Failed Step-2, zero dispatch.
- `CapstoneSettingsIntegrationRunTests.cs`: dangerous candidate zero dispatch while ResetOptions visible.

---

**6. LEGACY EVIDENCE MATURITY VALUE:** NO (for S0), YES (for Phase 4)

Legacy E-10-C (search box misclassified by YOLO as menu_item) and E-09-L8 (subtitle double-click) are production-shaped perception problems. S0 synthetic environments provide perfect element types. The semantic model (CandidateAuthorizationEvidence) handles the authorization boundary, but real YOLO/OCR misclassification is a Phase 4 perception challenge, not a current semantic gap.

---

**7. OWNERSHIP / AUTHORITY FIT:** YES

Current owner: caller-injected CandidateAuthorizationEvaluator. Decision authority: `Agent` (dispatches only authorized candidates). No ownership change needed.

---

**8. PRIMARY CLASSIFICATION:** FROZEN_CAPABILITY_COVERED

---

**9. WHY**

- CandidateAuthorizationEvidence provides pre-dispatch navigability check
- Destructive/state-changing/unresolved candidates → zero dispatch, proven
- Capstone proves dangerous-candidate zero-dispatch while dangerous element visible
- Semantic model (presence ≠ navigability) is encoded in the authorization boundary
- Legacy misclassification evidence (YOLO/OCR errors) belongs to Phase 4 perception, not current semantic gap

---

**10. NEXT TREATMENT:** NONE (Phase 4 perception will exercise this under real conditions)

---

### SP-08 — Recovery Attempt Must Not Imply Error Resolution

**Title:** Recovery actions must not reset error history; error resolution must be confirmed by fresh observation

**Primary RD:** RD-06 (RecoveryAction != ErrorStateReset)

---

**1. REALITY REQUIREMENT**

Executing a recovery action must not automatically reset error state. Error resolution must be confirmed by fresh observation. If errors continue after recovery, the system must escalate rather than cycle indefinitely.

---

**2. CURRENT NORMATIVE REPRESENTATION**

- Architecture invariant I-9: Recovery is act→observe→verify→reconcile, not a single action.
- `RecoveryResult`: exactly 2 variants — `Verified` (recovery confirmed) or `Failed(Reason)` (explicit failure). Single attempt, no retry loop.
- `agent-recovery/spec.md`: verification MUST succeed against VerificationCriteria. Failure → `RecoveryResult.Failed(Reason)` → Run Failed. SHALL NOT assume dispatch success = recovery complete.
- The current model does not have "error counters" or "retry loops" — it has explicit verification. Recovery either passes verification or the run fails. There is no "silent retry" path.

---

**3. CURRENT SPEC COVERAGE:** FULL

Evidence: `agent-recovery/spec.md` SHALL requiring verification against criteria. SHALL NOT assume dispatch success = recovery. Failure → Run Failed (no retry).

---

**4. PRODUCTION BEHAVIOR COVERAGE:** FULL

Evidence: `Recovery.Recovery.cs`: `Verify` produces `RecoveryResult.Verified` or `Failed(reason)`. Single attempt. Agent fails the run on `Failed`.

---

**5. EXECUTABLE PROOF:** FULL

- `RecoveryVerificationFailureTests.cs`: exact reason string `"恢复验证失败：期望 [ForegroundApplication == Settings]，实际 Foreground=[Launcher], page=[Launcher]（seq=5）"`, no resume, no Action-3, 3 recovery events.
- `Unit/AgentRecoveryTests.cs`: `VerifyFailure_Unrecoverable`.
- `CapstoneSettingsIntegrationRunTests.cs`: Assertion 12 stop-extract (Popup at seq8 + drift at seq9 → Failed, nothing absorbed).

---

**6. LEGACY EVIDENCE MATURITY VALUE:** NO

Legacy E-04-B (consecutive errors across backtracks) is heavily FSM-internal. The current architecture does not have error counters or retry loops — it uses explicit verification. The legacy evidence documents an FSM-level bug that the current architecture structurally prevents through a different mechanism (verification, not counting).

---

**7. OWNERSHIP / AUTHORITY FIT:** YES

Current owner: `Recovery` (execution). Decision authority: `Agent` (run failure on Failed recovery). No ownership change needed.

---

**8. PRIMARY CLASSIFICATION:** FROZEN_CAPABILITY_COVERED

---

**9. WHY**

- I-9 (Recovery = act→observe→verify→reconcile) is the frozen norm
- RecoveryResult is binary: Verified or Failed — no silent-success path
- Verification failure → Run Failed, proven by dedicated scenario + unit tests
- Capstone stop-extract proves nothing is silently absorbed
- Legacy FSM-level error-counter bug is structurally prevented by the verification model

---

**10. NEXT TREATMENT:** NONE

---

### SP-09 — Same Intent, Different Execution Methods

**Title:** A task expressed as a desired outcome must not require a specific execution method when the outcome can be achieved differently

**Primary RD:** RD-07 (TaskIntent != ExecutionMethod)

---

**1. REALITY REQUIREMENT**

The system must accept task descriptions as desired outcomes. If the task provides explicit execution steps, the system must execute them as specified without reinterpretation. If the task provides only the desired outcome, the system must discover the execution method. These two input forms must not be conflated.

---

**2. CURRENT NORMATIVE REPRESENTATION**

- The current system receives `Goal` + `Plan` as separate, caller-injected inputs. Goal defines the desired outcome (evaluated by GoalEvidence). Plan defines the execution steps.
- No Intent→Goal or Intent→Plan synthesis exists. The system does not accept "raw intent" — it accepts pre-constructed Goal and Plan objects.
- `s0-graduation.md` explicitly lists "Intent → Goal / Plan implementation" as NOT authorized.
- Charter §1 states the system receives Intent → Plan → ..., but the Intent→Plan transformation is not implemented. Goal+Plan are caller-supplied.

---

**3. CURRENT SPEC COVERAGE:** NONE

Evidence: No spec currently requires the system to accept raw intent descriptions. No spec requires the system to derive execution methods from desired outcomes. No spec requires the system to distinguish "what to achieve" from "how to achieve it" at the input boundary.

---

**4. PRODUCTION BEHAVIOR COVERAGE:** NONE

Evidence: The system accepts Goal+Plan as separate objects. Both are caller-injected. The system executes the Plan to satisfy the Goal. If the caller provides different Plans for the same Goal, the system executes whichever Plan is given. But the system never derives a Plan from a Goal — the caller does that.

---

**5. EXECUTABLE PROOF:** NONE

No test exercises "same Goal, different Plans, same outcome." The test suite always pairs a specific Goal with a specific Plan.

---

**6. LEGACY EVIDENCE MATURITY VALUE:** YES — for future phase

Legacy E-14/E-15/E-16 (PlanCompiler, IntentExtractor, ScenarioPlanLoader) demonstrate two distinct plan construction paths in the old system. This evidence is directly relevant to future Intent→Goal/Plan synthesis work.

---

**7. OWNERSHIP / AUTHORITY FIT:** UNKNOWN

Current architecture does not have an Intent→Goal/Plan synthesis component. Adding one would require a new owner and authority model. This is explicitly deferred by S0_GRADUATED.

---

**8. PRIMARY CLASSIFICATION:** EXPLICITLY_DEFERRED_CAPABILITY

---

**9. WHY**

- Intent→Goal/Plan synthesis is explicitly NOT authorized by S0_GRADUATED
- Current system accepts pre-constructed Goal+Plan, not raw intent
- No spec, implementation, or test covers intent→execution derivation
- Charter §1 acknowledges the Intent→Plan boundary but defers implementation
- Legacy E-14/E-15/E-16 provide relevant evidence for future work

---

**10. NEXT TREATMENT:** DEFER_TO_FUTURE_MATURITY (Phase 5/6 Intent→Goal/Plan synthesis)

---

### SP-10 — Same Logical Page Must Be Recognized Across Observations

**Title:** Two observations of the same logical page must be recognized as the same page despite minor differences in observed elements

**Primary RD:** RD-08 (RawPageEvidence != SemanticPageIdentity)

---

**1. REALITY REQUIREMENT**

When the system observes a page whose elements are substantially similar to a previously-visited page, it must recognize the page as previously visited — not treat it as a brand-new unexplored page. Minor differences in element text, coordinates, or scroll offset must not prevent recognition.

---

**2. CURRENT NORMATIVE REPRESENTATION**

- `Container` identity: SemanticPageName (string) + `IsStillMine(Observation)` rule (caller-injected). The identity rule answers "does this observation still belong to my page?"
- `WorldBelief`: SemanticPage + Confidence + Evidence + SourceObservationSequence. Reconciled from observation, not assumed.
- `ViewportIdentityContinuityTests`: prove Container identity persists across observation changes (fresh seq, compatible foreground, IsStillMine, same reconciled page).
- Architecture invariant I-6: Fingerprint is evidence, not identity. Current implementation has no Fingerprint field (DEFER, 裁决 2).
- SC-P3-003 (Viewport Movement Preserves Container Identity): scroll actions must not change Container identity.

---

**3. CURRENT SPEC COVERAGE:** FULL

Evidence: `container-traversal/spec.md` requires Container identity via SemanticPageName + IsStillMine. `scroll-identity/spec.md` requires scroll actions to preserve identity.

---

**4. PRODUCTION BEHAVIOR COVERAGE:** FULL

Evidence: Container.Bind sets SemanticPageName + identity rule. Reconcile produces WorldBelief from observation. Agent compares reconciled page to current Container.

---

**5. EXECUTABLE PROOF:** FULL

- `ViewportIdentityContinuityTests.cs`: positive (same container, fresh evidence, progress preserved).
- `PopupObstructionRecoveryTests.cs`: continuous branch verified, rejected branch → Container Trap.
- `SiblingBranchProgressScenarioTests.RevisitA_IsIdempotent`: revisit recognized.
- `CapstoneSettingsIntegrationRunTests.cs`: 14 pages traversed with continuity verification across navigation + scroll.

---

**6. LEGACY EVIDENCE MATURITY VALUE:** YES

Legacy E-10-A (DFS revisit loop) is directly relevant. The engine re-entered the Internet page but failed to recognize it as previously visited, generating all navigation tasks anew. This is precisely the failure mode that Container identity + ExecutedSteps tracking prevents.

---

**7. OWNERSHIP / AUTHORITY FIT:** YES

Current owner: `Container` (SemanticPageName + IsStillMine), `World.Reconcile` (pure function). Decision authority: `Agent` (Container bind/rebind). No ownership change needed.

---

**8. PRIMARY CLASSIFICATION:** FROZEN_CAPABILITY_COVERED

---

**9. WHY**

- Container identity (SemanticPageName + IsStillMine) prevents unrecognized-revisit
- WorldBelief reconciliation separates observation from semantic conclusion
- Revisit idempotence proven by SiblingBranchProgressScenarioTests
- Legacy E-10-A DFS revisit loop is structurally prevented
- S1 replay of E-10-A would strengthen provenance

---

**10. NEXT TREATMENT:** ATTACH_LEGACY_EVIDENCE (S1 replay of E-10-A for revisit-recognition evidence)

---

### SP-11 — Goal Satisfaction Without Execution

**Title:** When the external world already satisfies a stated goal, the system must recognize satisfaction without executing unnecessary actions

**Primary RD:** RD-10 (GoalExpression != GoalState)

---

**1. REALITY REQUIREMENT**

When the world state already satisfies a goal (e.g., "Wi‑Fi is on" and Wi‑Fi is already on), the system must recognize satisfaction from current observation — not require execution of prescribed steps merely because they are associated with achieving the goal.

---

**2. CURRENT NORMATIVE REPRESENTATION**

- `GoalEvidence`: `Satisfied` + `Reason` + `SourceObservationSequence`. The evaluator can return Satisfied from any observation. No requirement that an action must have been dispatched first.
- Architecture invariant I-10: Completion requires Goal Evidence — but does not require action dispatch.
- The GoalEvidence evaluator is caller-injected. If the evaluator returns Satisfied from the initial observation, the Agent will complete without dispatching any actions.

---

**3. CURRENT SPEC COVERAGE:** NONE

Evidence: No spec SHALL-requires that the system must be capable of completing without dispatching actions when the goal is already satisfied. The semantic model permits it (GoalEvidence.Satisfied from any observation), but no normative requirement explicitly tests this boundary.

---

**4. PRODUCTION BEHAVIOR COVERAGE:** PARTIAL

Evidence: The Agent.RunAsync loop checks GoalEvidence after each observation. If the evaluator returns Satisfied from the initial observation, the Agent would complete. However, no production code path currently exercises this — the test suite always dispatches at least one action.

---

**5. EXECUTABLE PROOF:** NONE

- `GoalEvidenceCompletionTests.cs`: proves completion after dispatch + evaluation. Does NOT test completion from initial observation without dispatch.
- No test currently exercises: initial observation → GoalEvidence.Satisfied = true → Run Completed without any actions dispatched.
- The negative case IS tested: plan exhausted + unsatisfied → Failed.

---

**6. LEGACY EVIDENCE MATURITY VALUE:** YES

Legacy E-03-B (SimulationBaselineTests target search — TargetFound via structured field evaluation) demonstrates goal satisfaction from observation without re-asking AI. Legacy E-17 (ITraversalAdvisor stateless goal-as-string) demonstrates the conflation of goal expression with goal state.

---

**7. OWNERSHIP / AUTHORITY FIT:** YES

Current owner: caller-injected GoalEvidence evaluator. Decision authority: `Agent` (completes on Satisfied). Adding a no-dispatch-completion test would not change ownership.

---

**8. PRIMARY CLASSIFICATION:** SPECIFICATION_GAP

---

**9. WHY**

- Semantic model CAN express goal-satisfaction-without-execution (GoalEvidence.Satisfied from any observation)
- But no normative spec explicitly requires this behavior
- No executable test proves the zero-dispatch completion path
- The semantic capability exists; the normative requirement does not
- This is a specification gap, not a semantic model gap — no new concepts needed

---

**10. NEXT TREATMENT:** SPEC_RECONCILIATION (add normative SHALL + executable test for zero-dispatch goal satisfaction)

---

### SP-12 — Plan Validity Must Not Imply Execution Success

**Title:** A successfully constructed plan must not be treated as a guarantee of successful execution; plan-world divergence must be detectable

**Primary RD:** RD-11 (PlanConstructed != ExecutionGuaranteed)

---

**1. REALITY REQUIREMENT**

When a plan is internally valid but its assumptions about the world are wrong (coordinates don't match, targets are absent), the system must detect the divergence and not report success based on plan validity alone.

---

**2. CURRENT NORMATIVE REPRESENTATION**

- Architecture invariant I-5: Plan is hypothesis, not reality.
- Architecture invariant I-10: Completion requires Goal Evidence — plan exhaustion alone never completes.
- `TraversalStepResult`: Succeeded | Failed(Reason). Failed steps are explicit.
- `ActionResult`: Dispatched/TimedOut/Rejected — never proves world success.
- No "plan was dispatched → run succeeded" path exists. Plan steps can fail. Agent can escalate.

---

**3. CURRENT SPEC COVERAGE:** FULL

Evidence: `run-lifecycle/spec.md` SHALL requiring GoalEvidence for completion. Plan exhaustion prohibited as completion. `container-traversal/spec.md` SHALL requiring post-action Observe.

---

**4. PRODUCTION BEHAVIOR COVERAGE:** FULL

Evidence: Agent.RunAsync checks GoalEvidence after each step. Plan exhaustion → Failed, not Completed. Traversal steps return Failed(reason) on target-not-found, action-rejected, etc.

---

**5. EXECUTABLE PROOF:** FULL

- `GoalEvidenceCompletionTests.cs`: negative case — plan exhausted + unsatisfied → Failed "Plan 步数耗尽."
- `EscalationWithoutStealingAuthorityTests.cs`: missing target → Failed Step-2, no recovery actions.
- `CapstoneSettingsIntegrationRunTests.cs`: route not pre-encoded (Assertion 1). Plan-world mismatch handled by discovery (CAND-008).
- `AgentRecoveryLauncherDriftTests.cs`: Trap Expected=3/Observed=4 — divergence detected and trapped.

---

**6. LEGACY EVIDENCE MATURITY VALUE:** NO

This is architecture-level invariant coverage. I-5 and I-10 directly encode this distinction. Legacy evidence (E-14 PlanCompiler validation, E-16 coordinate mismatch) documents legacy patterns that the current architecture already prevents.

---

**7. OWNERSHIP / AUTHORITY FIT:** YES

Current owner: `Agent` (GoalEvidence), `Traversal` (step execution). Decision authority: `Agent` (completion). Explicitly frozen: plan exhaustion ≠ completion.

---

**8. PRIMARY CLASSIFICATION:** FROZEN_CAPABILITY_COVERED

---

**9. WHY**

- I-5 (Plan is hypothesis, not reality) is the frozen architecture invariant
- I-10 (Completion requires Goal Evidence) is the frozen norm
- Plan exhaustion alone → Failed, proven by GoalEvidenceCompletionTests
- Plan-world mismatch → Failed, proven by EscalationWithoutStealingAuthorityTests
- This is the strongest example of architecture-level coverage in the corpus

---

**10. NEXT TREATMENT:** NONE

---

### SP-13 — Revisiting a Page Must Not Reset Exploration State

**Title:** Re-entering a previously visited page must preserve knowledge of what was already explored on that page

**Primary RD:** RD-09 (PreviouslyVisited != Unexplored)

---

**1. REALITY REQUIREMENT**

When the system returns to a page it has visited before, it must recall which navigation targets were already dispatched and which were not. It must not regenerate all navigation tasks as if the page were new, nor treat the page as fully exhausted merely because it was visited before.

---

**2. CURRENT NORMATIVE REPRESENTATION**

- `BranchProgressEvidence`: immutable snapshot — ApprovedSiblingEvidence (all known navigation targets) and CompletedSiblingEvidence (all proven exhausted). `WithCompletedSibling` produces a new snapshot. Idempotent — recording the same completion twice produces the same result.
- SC-P3-CAND-004: sibling progress tracking across Container instances.
- SC-P3-CAND-005 (Recovery Progress Resume): `PlanStep.BranchEffectEvidenceEvaluator` revalidates recovered-world branch effects. True = revalidated (progress preserved). False = contradicted (progress excluded). Null = unresolved.
- SC-P3-CAND-009 (Discovered Branch Effect Revalidation): `BranchEffectCriterion` revalidates discovered non-Plan branches after recovery.

---

**3. CURRENT SPEC COVERAGE:** FULL

Evidence: CAND-004 spec requires idempotent progress recording. CAND-005 spec requires revalidation without blind replay. CAND-009 spec requires post-recovery revalidation of discovered branches.

---

**4. PRODUCTION BEHAVIOR COVERAGE:** FULL

Evidence: Agent records BranchProgressEvidence snapshots. Revisits produce the same snapshot (idempotent). Recovery revalidates rather than re-records.

---

**5. EXECUTABLE PROOF:** FULL

- `SiblingBranchProgressScenarioTests.RevisitA_IsIdempotentAndDoesNotCreateDistinctProgress`: revisits don't fabricate new progress.
- `RecoveryProgressResumeScenarioTests.cs`: positive (A revalidated > boundary, B continues); contradicted (A excluded → Failed); unresolved (A stays at ≤boundary → Failed).
- `DiscoveredBranchEffectRevalidationScenarioTests.cs`: CAND-009 historical completion preserved at ≤boundary, revalidated only after VERIFIED recovery, zero duplicate dispatch.
- `CapstoneSettingsIntegrationRunTests.cs`: assertions 6/7/8 — re-entry not new progress (Network completion 18→21).

---

**6. LEGACY EVIDENCE MATURITY VALUE:** YES

Legacy E-10-A (DFS revisit loop — re-enters page, treats as new) and E-07 (hub revisited after first branch, second branch undispatched but page treated as "done") both exercise the revisit-without-reset pressure. The current model handles both.

---

**7. OWNERSHIP / AUTHORITY FIT:** YES

Current owner: `Agent` (BranchProgressEvidence snapshots). Decision authority: `Agent` (completion). No ownership change needed.

---

**8. PRIMARY CLASSIFICATION:** FROZEN_CAPABILITY_COVERED

---

**9. WHY**

- BranchProgressEvidence idempotence prevents revisit-as-new
- CAND-005 revalidation prevents blind progress replay after recovery
- CAND-009 revalidation handles discovered branches
- Capstone assertions 6/7/8 prove re-entry doesn't fabricate progress
- Legacy E-10-A revisit loop + E-07 premature completion are both structurally prevented

---

**10. NEXT TREATMENT:** ATTACH_LEGACY_EVIDENCE (S1 replay of E-10-A + E-07 for revisit-preservation evidence)

---

## Established Territory

The following 10 Scenario Pressures are genuinely frozen and proven by the current Runtime:

| SP | Frozen Capability | Key Proof |
|---|---|---|
| SP-01 | Startup foreground verification (Phase 1) | StartupForegroundVerificationFailureTests (6 assertions) |
| SP-02 | Traversal Observe→Verify + uncertain action (Phase 1/3) | UncertainActionVerificationTests, PopupObstructionRecoveryTests |
| SP-03 | BranchProgressEvidence (CAND-004) + BranchInventoryEvidence (CAND-008) | SiblingBranchProgressScenarioTests, Capstone (14 pages) |
| SP-04 | BranchInventoryEvidence depth bound (CAND-008) | BoundedCrossPageDiscoveryScenarioTests (depth 0/1/2) |
| SP-06 | ViewportExplorationEvidence bound (CAND-007) | ViewportExplorationScenarioTests (positive/ambiguous/bound-reached) |
| SP-07 | CandidateAuthorizationEvidence (CAND-006) | BoundedCandidateSafetyScenarioTests (destructive zero-dispatch) |
| SP-08 | RecoveryResult verification (Phase 2) | RecoveryVerificationFailureTests (exact reason string) |
| SP-10 | Container identity + WorldBelief (Phase 1) | ViewportIdentityContinuityTests, RevisitA_IsIdempotent |
| SP-12 | I-5 (Plan is hypothesis) + I-10 (Goal Evidence) | GoalEvidenceCompletionTests negative, EscalationWithoutStealingAuthorityTests |
| SP-13 | BranchProgressEvidence idempotence (CAND-004/005/009) | RevisitA_IsIdempotent, RecoveryProgressResumeScenarioTests, Capstone |

These 10 pressures did not reveal any new Reality Distinction — the current Runtime already encodes and proves the required semantic boundaries.

---

## S1 Evidence Upgrade Portfolio

Three Scenario Pressures are semantically covered but should consume legacy recorded replay/integration evidence at S1:

| SP | Current Coverage | Legacy Evidence to Attach | What S1 Would Prove |
|---|---|---|---|
| SP-03 | BranchProgressEvidence (synthetic) | E-07 (MultiBranchNavigation — unfixed bug) | Real false-completion failure reproduced against current model |
| SP-04 | BranchInventoryEvidence depth bound (synthetic) | E-11 (SettingsEnumerateRegression) + E-08 Step2 (depth=4) | Real depth-runaway failure reproduced against current depth bound |
| SP-05 | ViewportExplorationEvidence tri-state (synthetic) | E-13-B (ADB scroll failure → IsEnd=true) | Real observation-failure≠exhaustion under device-failure conditions |
| SP-06 | ViewportExplorationEvidence bound (synthetic) | E-12-A (scroll-only dead-end, K=3 stability) | Mechanism comparison: bound vs content-stability |
| SP-10 | Container identity (synthetic) | E-10-A (DFS revisit loop) | Real revisit-recognition failure reproduced |
| SP-13 | BranchProgressEvidence idempotence (synthetic) | E-10-A + E-07 (revisit loop + premature completion) | Real revisit-preservation failure reproduced |

These are evidence-maturity upgrades, not semantic deficiencies. The current model handles the distinctions; S1 would prove it under recorded-reality conditions.

---

## Composition Frontier

No COMPOSITION_PROOF_GAP classifications. The Capstone (SC-S0-CAPSTONE-001) already exercises multi-capability composition: 13 frozen capabilities composed in a single integration run (411/411 tests, 13/13 OpenSpec strict). The 7-conjunct GoalEvidence requirement (all branches complete, no dangerous dispatch, no unresolved branches, popup continuity, drift recovery verification, proven progress, deterministic replay) exercises the most challenging composition the current system can express.

---

## Semantic Frontier

No SEMANTIC_MODEL_GAP classifications. All 13 legacy-derived Scenario Pressures can be honestly represented by current Runtime semantics. The 11 Reality Distinctions from Step 4 are all expressible in the current model. No new concept, type, or semantic boundary is required.

---

## Architecture Frontier

No ARCHITECTURE_PRESSURE classifications. All 13 Scenario Pressures fit within the current ownership/authority model (Agent→Container→Traversal→Environment) without challenging frozen boundaries. No invariant (I-1 through I-14) is stressed by any SP.

---

## Explicitly Deferred Product Frontier

One Scenario Pressure belongs to a future product capability:

| SP | Deferred Capability | Phase | Current Status |
|---|---|---|---|
| SP-09 | Intent→Goal/Plan synthesis | Phase 5/6 | Not authorized by S0_GRADUATED. Current system accepts pre-constructed Goal+Plan. Legacy E-14/E-15/E-16 provide evidence for future work. |

---

## Specification Gap

One Scenario Pressure has a specification gap (not a semantic gap):

| SP | Gap | Fix |
|---|---|---|
| SP-11 | GoalEvidence.Satisfied from initial observation is semantically possible but not normatively required or tested | Add normative SHALL + executable test for zero-dispatch goal satisfaction |

---

## Candidate Pressures

NONE. No SEMANTIC_MODEL_GAP was found. No new Candidate needs to be registered.

---

## Architecture Review Pressures

NONE. No ARCHITECTURE_PRESSURE was found.

---

## Long-Term Target Alignment

Given the target: "An Agent can accept a high-level task intent and autonomously interact with GUI reality to achieve it safely, recoverably, and with honest completion."

The 13 Scenario Pressures ranked by contribution toward this target:

### FOUNDATION_ALREADY_ESTABLISHED (current S0 proves these)

| Rank | SP | Why Foundation |
|---|---|---|
| 1 | SP-12 | Plan≠Reality + GoalEvidence completion is THE honest-completion foundation. Without this, all other pressures are moot. |
| 2 | SP-03 | Multi-branch honest completion is the most direct test of "did we actually finish?" The legacy E-07 bug is the canonical false-completion case. |
| 3 | SP-01 | Entry verification is the safety boundary. Wrong-app traversal makes all subsequent work meaningless. |
| 4 | SP-08 | Recovery verification (act→observe→verify→reconcile) is the recoverable-operations foundation. |

### NEXT_EVIDENCE_MATURITY (S1 replay would strengthen)

| Rank | SP | Why S1 Matters |
|---|---|---|
| 5 | SP-05 | Observation-failure≠exhaustion is the most important real-device distinction. The legacy E-13-B gap is P0 severity. S1 replay of real ADB failure would be the strongest single evidence upgrade. |
| 6 | SP-10 | Page-recognition across observations is critical for autonomous navigation. S1 replay of the legacy DFS revisit loop would prove the identity mechanism under recorded reality. |
| 7 | SP-04 | Depth-bound enforcement during discovery is structural. S1 replay of the legacy depth=4 runaway would prove the bound holds under real conditions. |

### NEXT_SEMANTIC_FRONTIER (small spec gap, easily closed)

| Rank | SP | Why Next |
|---|---|---|
| 8 | SP-11 | Goal satisfaction without execution is the smallest gap to close (spec + test, no new concepts). It directly advances "autonomously achieve" by proving the system doesn't over-execute. |

### FUTURE_PRODUCT_CAPABILITY (deferred to later phases)

| Rank | SP | Why Future |
|---|---|---|
| 9 | SP-09 | Intent→Goal/Plan synthesis is the bridge from "high-level task intent" to executable Goal+Plan. This is the most important deferred capability for the long-term target — but it is correctly deferred, not a current gap. |

The remaining SPs (SP-02, SP-06, SP-07, SP-13) are already foundation-established and tested through the Capstone composition.

---

## Recommended Next Decision

**PROCEED_WITH_S1_REPLAY_PORTFOLIO**

**Why:**

1. **Dependency order.** The S1 evidence-upgrade portfolio (SP-03, SP-04, SP-05, SP-06, SP-10, SP-13) depends only on S0 frozen capabilities — no new semantics, no architecture changes. It can proceed immediately under PROJECT_LEADER_S1_AUTHORIZATION.

2. **Highest-value single upgrade.** SP-05 (observation failure ≠ content exhaustion) has the strongest legacy evidence (E-13-B, P0 severity documented gap in production code) and the weakest current S0 coverage (partial spec + partial behavior + partial proof). S1 replay of real ADB failure would be the single most valuable evidence upgrade.

3. **Spec gap is trivial.** SP-11 (goal satisfaction without execution) is a one-test specification gap — add a normative SHALL + an executable test case. It can be closed in parallel with S1 replay work. It does not block anything.

4. **No semantic frontier.** Zero SEMANTIC_MODEL_GAP or ARCHITECTURE_PRESSURE classifications means no Human Gate is required before S1 replay. The current model is sufficient.

5. **Deferred capability is correctly deferred.** SP-09 (Intent→Goal/Plan synthesis) belongs to Phase 5/6. Attempting it now would violate the S0_GRADUATED authority boundary and the Phase Boundary Discipline.

6. **S1 replay directly serves the long-term target.** The evidence-maturity upgrades (proving current semantics under recorded reality) are the next logical step toward "autonomously interact with GUI reality" — they bridge the gap between synthetic S0 proof and real-world evidence without requiring new architecture.

---

## Repository Changes

`docs/decisions/legacy-spec-architecture-challenge-step6.md` ONLY
