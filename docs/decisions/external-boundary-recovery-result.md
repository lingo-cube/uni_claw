# PROJECT_LEADER_EXTERNAL_BOUNDARY_RECOVERY_RESULT

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Task: PROJECT_LEADER_EXTERNAL_BOUNDARY_REALDEVICE_RECOVERY_FIX — fix
> `ExternalBoundary_RealDevice` using the AI Coding Evidence-Driven Workflow
> (protocol §17; `evidence-driven-debugging` skill).
>
> **Discipline applied**: uiautomator/XML is AUXILIARY ANALYSIS only (not a flow
> component — the observation carries the primary OCR channel and nothing
> else); defects exposed by the investigation are REPORTED to their owning
> layer, not papered over with one-shot harness workarounds.
>
> **AuthorityDelta: NONE — ArchitectureDelta: NONE.** Zero Runtime production
> code changed; Agent/FSM/Traversal/GoalEvidence/Semantic/Vision-first/ADB-
> auxiliary contracts untouched.

---

## 1. Evidence Summary

E4 evidence (multiple real-emulator runs; `/tmp/ebd_real_evidence.txt`,
`/tmp/ebd_obs_*.xml`):

- **Original failure: Discovery** — `Source normalization is unresolved` at
  completeness. The accepted scroll frames' navigation evidence violates the
  frozen NORM4 ordered-overlap contract: per-frame duplicates ("Q Search
  settings" ×3-4, rows ×2-3), unstable perception types (`menu_item` ↔
  `text_block`), unstable OCR text for one row ("Notification history,
  conversations" ↔ "…,conversations"), phantom detections ("LoO"/"Lo"/"Lou"
  — location-pin icon read as text; a row detected at the top edge but missed
  in the previous frame), garbled text ("Securitv&nrivacy"). The normalizer
  correctly fails closed.
- **Current failure: Execution** — after OCR-only evidence assembly fixed the
  normalization (every frame `normResolved=True`, 19-source inventory FROZEN,
  authorization rejects correct), the dispatch tap on the OCR-identified
  "location" row **mis-navigates**: the OCR box sits at y≈0.80-0.83 while the
  real row is at y≈0.541-0.578 (~500px error) — the tap opens "Safety &
  emergency" (post-tap device frames) → settle fails closed.
- Capstone real-device remains green; deterministic EBD-related suites 55/55.

## 2. Failure Classification

- **B — test assumption mismatch** (the harness assumed clean, stable,
  screen-consistent OCR evidence on the dense Settings list; it is not).
- **C — environment/AVD OCR behavior** (the vision provider's detection
  stability and box accuracy on this AOSP Settings screen).
- NOT A (Runtime defect): every Runtime fail-closed behavior was correct;
  NOT D (missing wiring): the capability/harness produces evidence; the
  evidence-contract friction (E) is real and correctly fail-closed.

## 3. Root Cause

The real-device primary OCR channel cannot satisfy the frozen NORM4
ordered-overlap contract on the dense Settings list (duplicates / text+type
instability / phantoms / garbles), and its bounding boxes are ~500px imprecise
for taps. The Runtime is correct; the EBD test's evidence assumptions and page
resolver were the fixture-side mismatches; the tap-precision is a perception-
layer defect.

## 4. Ownership Decision

- **Environment / perception layer**: OCR detection stability + bounding-box
  accuracy (exposed defects D1/D2).
- **Test fixture**: evidence assembly (OCR-only normalization) + Vision-first
  page resolver (fixed here).
- **NOT the Runtime**: no Runtime mechanism changed or defective.

## 5. Change Summary

Test-side only (`tests/.../Scenario/ExternalBoundaryTests.cs`):

| change | purpose |
|--------|---------|
| Harness `ObserveAsync` — **Vision-first**: the observation carries ONLY the normalized primary OCR channel; uiautomator XML is auxiliary analysis only (live foreground, root marker, device-state collection — never injected) | remove the dual-channel evidence + XML-as-flow-component |
| OCR-only evidence assembly: one occurrence per distinct canonical text (lowercased, whitespace/punct stripped), stable "row" type, screen order from OCR bounds, fragments (<4 chars) and icon-typed overlays dropped, search-bar/title anchor excluded, top-edge row excluded | make the multi-frame ordered-overlap normalization pass with the real device |
| Canonical-aware consumers: fixture classifier (`searchsettings` suffix), `AuthorizeEbdReal`, `EbdViewportExploration` (canonical labels) | keep label matching consistent with canonical evidence |
| EBD-specific Vision-first `EbdResolveSemanticPage` (OCR sub-page title + auxiliary root marker) | replaces the reused structured-dependent resolver (D3) |
| **Removed** (per discipline): XML-based tap-coordinate refinement, structured interaction-flag neutralization | the harness must not compensate perception defects via uiautomator coordinates |

## 6. AuthorityDelta

**NONE** — no execution authority added; Agent / FSM / GoalEvidence decision
surface untouched.

## 7. ArchitectureDelta

**NONE** — test-harness changes only; DFS, Traversal, Semantic Capability,
Vision-first, ADB auxiliary-only contracts untouched; normalizer standards
unchanged (fail-closed preserved).

## 8. Regression Result

- Deterministic EBD-related suites (ExternalBoundary, AdaptiveRevisitCoverage,
  SourceEquivalenceNormalizer, SourceProvenanceContract,
  SettingsSingleRecursiveChild): **55/55 PASS**.
- Capstone real-device: **PASS** (unchanged).
- `check-consistency.sh`: ALL PASS; `git diff --check`: clean.
- Full suite: (not re-run this turn; EBD-local test changes only — see
  Remaining Risk for the final full-regression note).

## 9. Remaining Risk / Escalation

The EBD real-device flow remains **BLOCKED at dispatch by the exposed
perception defect D2** (OCR box imprecision → tap mis-targeting). Per the
working discipline, this is REPORTED — not compensated in the harness:

| # | exposed defect | owner | recommended direction (not performed here) |
|---|----------------|-------|---------------------------------------------|
| D1 | dense-list OCR violates NORM4 ordered-overlap (duplicates/text+type instability/phantoms/garbles) — test-side assembly mitigates; production capability faces the same reality | perception / semantic-capability layer | capability-level detection stabilization; normalizer stays fail-closed |
| D2 | OCR boxes ~500px off → taps hit adjacent rows | perception layer (vision provider box accuracy) | box-accuracy investigation/calibration; decide the EBD real-device validation scope (e.g., tap-target tolerance or a scoped evidence channel) |
| D3 | EBD reused a structured-dependent page resolver | test fixture | fixed here (Vision-first resolver) |

Full-suite regression + the D2 resolution decision are the next steps for the
owning layer; the harness will not work around perception defects.
