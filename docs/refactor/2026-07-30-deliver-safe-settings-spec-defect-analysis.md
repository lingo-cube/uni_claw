# deliver-safe-android-settings-test-loop — Spec Design Defect Analysis

> Date: 2026-07-30
> Object: `openspec/changes/deliver-safe-android-settings-test-loop/` (proposal, design, 4 specs, tasks, evidence)
> Question: do the specs have design defects, not just implementation gaps?
> Method: cross-read specs ↔ design ↔ evidence ↔ implementation. Each finding cites the spec/design/evidence line and grades severity by whether it can be fixed inside this change's apply vs. needs a new/changed proposal.
> Companion: `2026-07-30-host-implementation-map.md` (implementation gaps) and `2026-07-30-current-internal-gaps-calibrated.md` (gap inventory).

## Verdict

The spec set is **largely sound and unusually disciplined** — fixed deny precedence, versioned immutable inputs, honest-completion vocabulary, append-only issues, and a no-bypass safety gate are all correctly specified. But it has **six real design defects**: three that the evidence already proved bite in practice, two latent seams that task 8/9 will hit, and one structural omission about the runner/engine relationship. None requires scrapping the change; all six can be fixed by editing specs + a modest proposal delta. They are listed most-severe first.

---

## D1 — Verification spec silently licenses a fragile heuristic (SEVERE, evidence-proven)

**Where:** `android-settings-scenario-catalog/spec.md` Requirement "Locate-one-item scenario has a bounded success contract", Scenario "Target page is verified": success when "the post-action page title or visible identity **matches** the target or an alias."

**The defect:** the spec says "page title or visible identity matches" but never constrains *how* a match is proven. It does not forbid a fallback that declares success on indirect evidence. The evidence shows the implementer read this slack as license to add `LooksLikeVisualTransition` — declaring success when the *screenshot byte length* changed by ≥20%, even though the UIAutomator hierarchy still described the old page (`evidence.md:217-225`, `IncrementalScenarioRunner.cs:655-671`).

This is a **spec defect, not just a code smell**: the spec's "title or visible identity matches" wording admits an interpretation where "visible identity" is loose enough to mean "something visibly changed." The result was a false `success` (`20260729T200940861Z-bf24ff268b9b4df`, `target_page_visual_transition_verified`) that the spec was powerless to prevent.

**Severity rationale:** this directly violates the change's own Non-Goal ("不以首次运行全绿为目标") spirit and the run-artifact spec's "Final result never overstates completion" — but it does so *within the letter* of the locate spec. That gap between letter and intent is the defect.

**Fix (spec edit, in-change):** tighten the locate success scenario to: "success when the post-action `PageAnalysis` page identity equals the target or an alias, or — *only when the analysis provider returns a page identity* — the analysis-confirmed page identity matches. A screenshot-size change alone SHALL NOT satisfy target verification; an analysis/hierarchy/identity stale-read is a `verification_mismatch` or `stale_hierarchy` issue, never a silent success." Add an explicit `stale_hierarchy` failure vocabulary entry (see D2). This forbids the heuristic at the spec level so the code fix (Host map §6.5) is spec-backed, not discretionary.

---

## D2 — Failure classification vocabulary is specified but not closed (SEVERE, evidence-proven)

**Where:** `iterative-device-test-runner/spec.md` Requirement "Device and provider failures remain distinguishable" enumerates a long list (device unavailable, ADB timeout, screenshot failure, UI hierarchy failure, provider timeout, provider response invalid, planning failure, safety blocked, action failure, verification failure, budget exhausted, cancellation, trace/reporting failure).

**The defect:** the list is presented as a *fixed enumeration* but the design and evidence both produce classifications the spec never named:
- The evidence introduces `stale_plan` (`IncrementalScenarioRunner.cs:208`) and a "mid-wait disconnect" that the evidence itself flags as *not yet classified* ("currently reports this as an entry timeout rather than a distinct mid-wait disconnect", `evidence.md:198-199`).
- The code has a `stale_hierarchy` reality (the About page returns a stale UIAutomator tree) that the spec's "UI hierarchy failure" was not designed to cover — "UI hierarchy failure" reads as *capture/parse* failure, not *stale-but-valid* hierarchy.
- `target_absent_at_verified_end` (`IncrementalScenarioRunner.cs:418`) and `step_budget_exhausted` / `duration_budget_exhausted` are distinct from the spec's single "budget exhausted."

**Why this is a design defect:** a spec whose entire value is "failures stay distinguishable, lower-layer never converts to success" must *close* the vocabulary. An open enumeration invites the exact drift the evidence documents — implementers invent names (`stale_plan`, `target_absent_at_verified_end`) that the spec neither blesses nor forbids, so two readers can't tell whether a classification is spec-conformant. The run-artifact spec does say "using a versioned string vocabulary," but no spec defines that vocabulary's membership.

**Fix (spec edit, in-change):** add a "Classification vocabulary" requirement that makes the enumeration a *normative table* with a default-disposition rule: "any classification not in this table SHALL be treated as `unclassified` and recorded as a defect against this spec, not as a new ad-hoc status." Add the missing names: `stale_plan`, `stale_hierarchy`, `target_absent_at_verified_end`, `entry_mid_wait_disconnect`, and split `budget_exhausted` into `step_budget_exhausted` / `duration_budget_exhausted`. This makes drift visible instead of silent.

---

## D3 — Enumerate spec has no contract for "all entries accounted" (SEVERE, will block task 8)

**Where:** `android-settings-scenario-catalog/spec.md` Requirement "Safe-enumeration scenario is limited to discoverable first-level entries": enumerate unique entries until verified end-of-list or budget, sample each safe entry, skip dangerous ones. `design.md` §5 adds: unique key = normalized text + optional resource-id + home page identity; coordinates are not identity.

**The defect:** the spec defines *discovery* (scroll to end, dedup) and *per-entry sampling*, but it **never specifies the completion accounting contract** — i.e., what the result must prove to claim coverage. It says "MUST NOT report exhaustive enumeration" when end-of-list can't be proven, but it does not say what a *successful* enumeration's result must contain: the full set of discovered identities, which were sampled vs. skipped vs. failed-on-reentry, and a reconciliation that `discovered == sampled + skipped + failed`. Without this, an implementation can report `success` having sampled 3 of 12 entries as long as it "reached end of list."

The tasks clearly *expect* this accounting (8.1: "verified end-of-list accounting"; 8.3: "discovered-but-skipped accounting") — but the spec behind them doesn't mandate the reconciliation. That is a gap between task and spec.

**Why this is a design defect:** task 8's acceptance ("prove denied targets never reach the ADB runner", 8.3) and the stability gate (task 9: 10/10 "honest completion outcomes") both depend on an accounting contract the spec doesn't state. A reviewer cannot reject an implementation that omits the reconciliation, because the spec doesn't require it.

**Fix (spec edit, in-change):** add a "Enumeration completion reconciliation" requirement: "A successful enumeration result SHALL include the ordered set of discovered first-level identities and, for each, a disposition of `sampled`, `skipped` (with deny rule ID), or `failed` (with reason). The result SHALL satisfy `discovered == sampled ∪ skipped ∪ failed`. Reaching end-of-list without this reconciliation SHALL NOT produce a successful result." This closes the gap and makes 9.3's "honest completion" gate enforceable.

---

## D4 — Observation dual-path has no spec (MEDIUM, latent seam)

**Where:** no spec. The implementation has two observation paths — UIAutomator rule parse (mock provider) and AI `IPageAnalyzer` (`ScenarioObservation.cs:86-91`) — selected by `providerId == "mock"`. The specs speak only of "a normalized page analysis" (`iterative-device-test-runner/spec.md` "observe-plan-gate-execute-verify") and "page analysis cannot validate the provider response" (`iterative-device-test-runner/spec.md` "Provider response is invalid").

**The defect:** the spec treats `PageAnalysis` as a single thing, but the implementation produces it from two structurally different sources with no contract that they agree. The run-artifact spec's "Final result never overstates completion" assumes the analysis is trustworthy; nothing guarantees the mock/UIAutomator path and the AI path yield compatible `Items`/`HasScroll`/`IsEndOfList`. This is the deepest latent seam: emulator smoke tests (deterministic provider) are the "deterministic main regression" (`design.md` §7), yet they exercise a path that may not represent the real provider's analysis shape.

**Why this is a design defect:** the change's regression strategy (mock-first emulator smoke as the deterministic regression, real provider as protected explicit layer) is only valid if the two observation paths are contractually equivalent on the fields the runner and safety gate consume. The specs never assert this, so the strategy rests on an unstated assumption.

**Fix (spec edit, in-change):** add to `iterative-device-test-runner/spec.md` a "Observation source equivalence" requirement: "When the run supports more than one analysis source (e.g., deterministic UIAutomator and a vision provider), each source SHALL produce a `PageAnalysis` whose `CurrentPath`, `HasScroll`, `IsEndOfList`, and `Items` (text + coordinate + type) follow a shared shape contract. The runner SHALL verify source equivalence on shared fixtures; a source whose output shape diverges SHALL be rejected as a provider-response failure, not silently substituted." This makes the mock-first strategy honest.

---

## D5 — Safety-gate spec vs. AI safety capability: no integration contract (MEDIUM, latent)

**Where:** `deterministic-action-safety/spec.md` is explicit and correct: "AI safety judgment can only increase denial, not allow a deterministic denial" (`design.md` §4); "Deny precedence and default-deny"; "AI output MUST NOT override a deny."

**The defect:** this is correctly *restrictive*, but the spec gives no contract for the *positive* direction — when an AI safety capability (G1 `ScreenSafetyAsync`, currently `NotImplementedException`) lands, how does it feed the gate? The spec says AI can add denials, but doesn't specify: which candidate fields the AI safety result attaches to, whether AI denial produces a distinct `ruleId` namespace, or whether AI "not-safe" upgrades a `deny.default` to a named deny. Today the gate is purely static (`SettingsSafetyEvaluator`); the spec reads as if that's the entire design.

**Why this is a design defect (latent):** task 8.3's "discovered-but-skipped" accounting for *dangerous* entries currently relies solely on static `DangerousSemantics`/`DangerousText`. That works for Settings first-level (the dangerous items are lexically obvious — reset, erase, etc.). But the calibrated gaps (G1) plan a `screen-safety` slice precisely to handle entries the static lists miss. When that slice lands, the safety spec has no seam to receive it — forcing an implementer to either bypass the gate or bolt on an ad-hoc path, exactly the drift the spec exists to prevent.

**Fix (spec edit, deferred — can wait until the G1 slice, but record now):** add a "SafetyEvaluator composition" requirement that names the static evaluator as the *base* layer and reserves a `ruleId` namespace prefix (e.g., `deny.ai.*`) for an AI advisory layer that may only *add* denials. Mark it as a future extension point in this change's spec so the G1 `screen-safety` slice has a spec-defined seam instead of inventing one. This can be added now without implementing it; reserving the seam is cheap and prevents future drift.

---

## D6 — Runner/Engine relationship unspecified (MEDIUM, structural omission)

**Where:** no spec. The design (`design.md` §3) describes "incremental short plans" compiled from a `TraversalPlan`, and the evidence confirms `TraversalPlan` compilation (`evidence.md:165`). The `HostRunServices` even exposes `CreateTraversalEngine` (`HostCommands.cs:655-666`). But the `IncrementalScenarioRunner` implements its own observe→plan→verify loop and **never uses `TraversalEngine`/`TraversalFSM`** (Host map §6.6).

**The defect:** the specs never say whether the scenario runner *is* the traversal engine, *uses* it, or *replaces* it. `iterative-device-test-runner/spec.md` specifies the step loop directly (observe-plan-gate-execute-verify) without referencing the existing `traversal-engine`/`traversal-fsm` canonical specs. This leaves three things undefined:
1. Whether the runner's loop is a *replacement* for `TraversalFSM` or a *consumer* of it.
2. Whether Phase 3 behavior hardening (calibrated gaps G4 — preconditions, dangerous-action screening) applies to the runner path or only the FSM path.
3. Whether `HostRunServices.CreateTraversalEngine` is dead code, a future hook, or an intended second path.

**Why this is a design defect:** this is the single biggest reason the layer resists understanding (Host map §6.6), and it's a *spec* omission: the change adds an iterative-runner capability that overlaps the existing traversal-engine capability without stating their relationship. A reader assumes the runner uses the engine; it does not. The `traversal-fsm` canonical spec and this change's runner spec coexist with no contract between them, so G4 hardening has no defined target.

**Fix (spec edit, in-change):** add a "Runner and traversal-engine relationship" requirement stating the intended scope explicitly: "The scenario runner SHALL own the observe→plan→gate→execute→verify step loop for V1. `TraversalEngine`/`TraversalFSM` are not on the scenario-runner critical path in V1; their canonical specs remain authoritative for the plan-compiled engine path. If a future change routes the runner through `TraversalEngine`, it SHALL update both this spec and the `traversal-engine` canonical spec." This is a one-paragraph clarification that converts a structural omission into a documented decision, and it scopes G4 correctly (FSM path only, until a future change unifies).

---

## Non-defects (things the spec gets right, for contrast)

- **Deny-overrides-allow and default-deny** (`deterministic-action-safety/spec.md`): correctly fixed and stated; the evidence confirms the implementation honors it (`SafetyGate.cs:94-171`).
- **Immutable scenario snapshot + content hash** (`android-settings-scenario-catalog/spec.md`): correctly specified and implemented; the snapshot/hash pattern is sound.
- **Issues append-only with stable fingerprints** (`run-artifact-reporting/spec.md`): correctly specified; the JSONL one-line-per-record fix (`evidence.md:113-115`) was an implementation correction, not a spec gap.
- **No locked enum/interface changes** (design §"Decisions", evidence): the change's discipline of adding no `TraversalState`/`TypeHint`/`SelectionState` values and using versioned string vocabularies is correctly maintained — D2 is about closing the vocabulary, not about having used strings (strings were the right call).
- **Honest completion vocabulary** (`run-artifact-reporting/spec.md` "Final result never overstates completion"): the intent is correct; D1/D3 are about the specs that *undercut* this intent, not about this requirement itself.

---

## Recommended Fix Order

All six are spec edits; severity gates the order:

1. **D1** — forbid screenshot-size-as-success in the locate spec. Unblocks honest task-9 gates. (pairs with Host map §6.5 code fix)
2. **D2** — close the classification vocabulary with a default-disposition rule and add the missing names. Makes drift visible.
3. **D3** — add the enumeration completion reconciliation requirement. Unblocks task 8.1/8.3/9.3 acceptance.
4. **D4** — add the observation source-equivalence requirement. Makes the mock-first regression strategy honest before task 8.4 E2E tests are trusted.
5. **D6** — document the runner/engine relationship. Scopes G4 and removes the biggest readability gap.
6. **D5** — reserve the AI safety `ruleId` namespace seam. Cheap now, prevents drift when the G1 `screen-safety` slice lands.

These are spec deltas to an *active, partially-applied* change. The clean path: open a small `proposal delta` / spec amendment under the existing change (not a new change) before continuing tasks 8/9, because D1/D2/D3 directly shape what "done" means for tasks 8 and 9. Applying those tasks against the un-amended specs would bake in the false-success and open-vocabulary drift the evidence already surfaced.

---

## Verification Note

This analysis is static (spec + design + evidence + source cross-read), no `dotnet` execution. The two evidence-proven defects (D1, D2) cite `evidence.md` lines and `IncrementalScenarioRunner.cs` lines that were read directly; the latent defects (D3, D4, D5, D6) are argued from spec text and implementation structure, not from a run. If a `.NET`-enabled host later shows an implementation already reconciles D3's accounting, that would downgrade D3 from "defect" to "unspecified-but-honored" — but the spec gap remains and should still be closed.