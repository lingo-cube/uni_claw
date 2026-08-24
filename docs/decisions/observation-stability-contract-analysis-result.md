# PROJECT_LEADER_OBSERVATION_STABILITY_CONTRACT_ANALYSIS_RESULT

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Task: PROJECT_LEADER_OBSERVATION_STABILITY_CONTRACT_ANALYSIS — analyze the
> Observation acceptance / stability contract gap behind the ExternalBoundary
> real-device failure. **Analysis only; no code changed.** The previous
> iteration's fresh-bounds dispatch re-observe was REVERTED (U2OpenWorld suites
> re-verified green, 22/22) pending this higher-level stability-owner decision.
>
> **AuthorityDelta: NONE — ArchitectureDelta: NONE.**

---

## 1. Evidence Summary

- OCR detection and coordinate transforms verified CORRECT (rest-frame
  OCR-vs-settled offset ≈ 0; server `preprocess`→`remap_coords` math correct).
- The error is a temporal frame mismatch: the screenshot is captured while the
  Settings list is still settling after a scroll; the accepted decision frame
  carries mid-settle bounds; by tap execution time the list settled to a
  different layout and the tap hit the row below ("Safety & emergency").
- Offsets grow with scroll depth (rest ≈ 0 → +0.04 → +0.07 → +0.10-0.11), the
  temporal signature of scroll motion, not a detection/transform defect.
- `SettlePostScrollEvidenceQualityAsync` fired NO re-observe (bounds were
  valid) — there is no stability criterion in the acceptance path.

## 2. Current Lifecycle Analysis

Raw (screenshot, time T) → post-scroll evidence-quality settle (bounds-valid
only) → same-Container continuity → `AcceptFreshObservation` (becomes
CurrentObservation / decision frame) → grounding + dispatch on that frame.

Acceptance conditions today: exploration new-source; bounds-valid; same
container; NORM4 ordered overlap at completeness. **Missing: time/stability
(bounds-stability) — no check that the screen stopped moving.** The step that
wrongly promotes a mid-settle frame to "stable" is the post-scroll acceptance
path (`SettlePostScrollEvidenceQualityAsync` → continuity → accept).

## 3. Failure Classification

**A — Observation acceptance 缺少稳定性判断** (primary), with **B —
Environment acquisition timing** (screenshot captured mid-settle) as the
contributing input reality. C (stale dispatch evidence) is the consequence, not
the root; D and E ruled out with evidence.

## 4. Ownership Decision

**Owner: Agent — exploration / observation-acceptance seam.** The Environment
provides raw frames; the "is this frame the STABLE decision frame" judgment is
an Agent-owned acceptance policy. Environment / Traversal / Semantic
Capability are not at fault and not the owner.

## 5. Architecture Impact

No new cross-layer contract required: the stability judgment is observable
within the Agent's existing acceptance seam (bounded confirmation re-observe +
identical navigation-signature set → stable). ADDITIVE Agent-internal policy;
no ownership / authority / contract change. A formal "Stable Observation"
acceptance marker is optional and Agent-internal if desired.

## 6. Recommended Next Step

Class 1 (current-layer mechanism gap): proceed to a minimal, scoped
implementation task —

1. Post-scroll acceptance adds a BOUNDED stability confirmation (second
   observation with identical viewport signature set → accept as STABLE;
   budget exhausted → existing fail-closed semantics; no scenario knowledge).
2. Deterministic coverage: a motion-physics EvidenceFixture (bounds/signatures
   shift between observations until the world reports stability).
3. Real-device re-verification: EBD (Location tap hits the correct row) +
   Capstone (parent return / revisit unaffected).
4. Fixture re-keying: U2OpenWorld scripted fixtures must key drift injections
   to events (actions/screens), not observation sequence numbers — any
   legitimate observation-cadence change breaks them.

## 7. Remaining Risk

- Stability confirmation adds observations to every scroll → the sequence-keyed
  scripted fixtures (and any cadence-sensitive world) need the re-keying above;
  the deterministic motion fixture must bound the confirmation budget.
- On devices where the list settles slowly (long flings), the confirmation
  budget may be insufficient — must fail closed, never dispatch on a
  knowingly-mid-settle frame.
- The stop-condition boundaries (Agent authority / Observation ownership /
  fail-closed / scenario knowledge / ADB-XML-primary) were respected; a fix
  must keep them.
