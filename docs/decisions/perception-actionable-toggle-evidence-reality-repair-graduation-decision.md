# Perception Actionable Toggle Evidence — Reality Repair Graduation Decision

| Attribute | Value |
|-----------|-------|
| Change | `perception-actionable-toggle-evidence-reality-repair` |
| Decision | **GRADUATED** |
| Maturity | `PERCEPTION_ACTIONABLE_TOGGLE_REALITY_REPAIR_INTEGRATED` |
| Buyer | `RAW_CONTROL_CANDIDATE_GENERATION_GAP` |
| Owning layer | `FUSION_CANDIDATE_GENERATION_GAP` |
| Review | Independent graduation review (2026-08-16), canonical repo state re-verified at `4d33837` before persisting |
| Review posture | No production mutation, no repair, no Runtime change, no popup/dsh touch, no YOLO training |

> **Supersession note.** This file previously carried the *parent change's*
> graduation record (Wi-Fi `GLOBAL_DISTANCE_ASSOCIATION_ASSUMPTION` repair,
> maturity `PERCEPTION_ACTIONABLE_TOGGLE_EVIDENCE_INTEGRATED`). That record was
> historically superseded by the live-reality falsification documented in
> `docs/decisions/perception-actionable-toggle-evidence-reality-falsification.md`.
> The parent change (`perception-actionable-toggle-evidence`) remains a SEPARATE
> ACTIVE OpenSpec stream and is NOT graduated or archived by this decision.

## 1. Buyer and original falsification

- **Buyer**: `RAW_CONTROL_CANDIDATE_GENERATION_GAP` — the fusion pipeline could
  not generate actionable toggle candidates when YOLO emitted no control-class
  detections (no `icon`/`switch`/`toggle`/empty-text candidate to infer from).
- **Live falsification (API35 Developer Options, Android 15 emulator-5554)**:
  YOLO output was 34× `text_block`, ZERO control classes; 4 real switches were
  rendered on screen; the pre-repair pipeline returned 0 switch candidates.
- **Owning layer**: `FUSION_CANDIDATE_GENERATION_GAP` — repair lives in the
  Python fusion layer (`platforms/perception/uniclaw_perception/fusion/`).

## 2. Repair footprint (independently verified)

Production (attributable to this change — commit `41e322f`):
- `platforms/perception/uniclaw_perception/fusion/heuristics.py` (+562) —
  raw-pixel toggle candidate detector + post-inference dedupe + knob validation.
- `platforms/perception/uniclaw_perception/fusion/engine.py` (+3) — passes the
  decoded image into `apply_toggle_inference_heuristic`.
- `platforms/perception/uniclaw_perception/server.py` (+1) — `image=proc_img`.

Supporting: `platforms/perception/tests/test_reality_repair.py`,
`test_toggle_inference.py`, `tests/fixtures/reality/*` (2 PNG + 2 GT + README).

**No attributable production changes** under `src/UniClaw.Runtime/`, Agent,
Binding, StateBeliefReducer, GoalEvidence, DriverHost, dsh-plugin-uniclaw, or
`semantic-run-popup-obstruction-integration` (verified by commit diff).

## 3. Durable falsification proof

Repo-owned assets (SHA-256 in `fixtures/reality/README.md`, verified):
- `developer-options-falsification.png` — 1080×1920, 2026-08-15T13:21:00Z.
  Independent whole-frame structural scan confirms **4 real switches**:
  `[992,241,1043,272]` ON (teal, knob right), `[1012,636,1063,667]` ON,
  `[1012,1400,1063,1431]` OFF (gray, knob left), `[1012,1808,1063,1839]` ON —
  matching the groundtruth exactly (GT produced by independent per-column
  luminance analysis, reproduced by the review's own independent scan; NOT by
  the fusion pipeline).
- `developer-options-scrolled2.png` — Debugging section; the frame contains 15
  rendered switches (14 in GT + the master "Use developer options" switch at
  y≈124 which the GT omits; pixel-verified teal track + white knob, ON). The
  pipeline discovers 13; s05 (clipped by its OCR row band) and s14 (no text
  row) correctly fail closed.

Groundtruth methodology is independent of the repaired algorithm (different
thresholds/scan; not derived from fusion output). Capture metadata sufficient
for provenance.

## 4. Detector class gap (fresh re-run, real YOLO)

Fresh production pipeline (real YOLO `best.pt`, real RapidOCR, real fusion):

```
developer-options-falsification.png:  YOLO {text_block: 34}  → 3 raw_pixel_toggle candidates (ON/ON/OFF)
developer-options-scrolled2.png:      YOLO {icon: 2, text_block: 27, switch: 1}  → 13 switch candidates
                                      (11 raw_pixel + 1 YOLO switch + 1 icon-path), all real
```

## 5. Would-fail-without-repair

Controlled pre-repair execution (same pipeline, raw-pixel path disabled — the
parent's icon/empty-text-only heuristic) on the falsification frame yields 0
switch candidates; with the raw-pixel path, 3. The pre-repair tree has no
toggle heuristic at all. **WouldFailWithoutRepair = YES.**

## 6. Raw-pixel candidates and bounds localization

| Candidate | boundsPx | GT | C#-oracle state (same frame) |
|-----------|----------|----|------------------------------|
| raw_pixel_toggle | [992,240,1044,273] | sw1 [992,241,1044,273] | ON |
| raw_pixel_toggle | [1010,634,1066,670] | sw2 [1012,636,1063,667] | ON |
| raw_pixel_toggle | [1010,1398,1065,1434] | sw3 [1012,1400,1063,1431] | OFF |

Bounds control-localized (52–56×33–36 px vs 52–53×32–33 px GT); not row-sized,
not OCR boxes; pass `enforce_geometry` (no diagnostics).

## 7. Candidate-count reconciliation ("13 vs 14")

Scrolled2: 13 raw regions detected → 1 (s09) suppressed by the
existing-YOLO-switch overlap guard → 12 raw controls → 1 (master y124) loses
row association to the pre-existing icon-path candidate → 11 raw candidates
emitted; final = 11 raw_pixel_toggle + 1 YOLO switch + 1 icon-path = 13
candidates, one per physical switch. RawPixelCandidateCount = 11 emitted
(12 post-guard controls; 13 pre-guard regions). YoloSwitchCandidateCount = 1.
BaselineOtherCandidateCount = 1. PreDedupeCandidateCount = 13.
PostDedupeCandidateCount = 13. **CandidateCountAccounting = PASS.**

## 8. Deduplication truth

One physical switch → one canonical candidate on both frames (pairwise IoU <
0.6); raw regions overlapping existing switch candidates suppressed; tightest
raw box kept; YOLO's own switch candidate (s09) preserved with good bounds
[1010,1024,1066,1059] ≈ GT [1012,1026,1063,1057].

## 9. Negative safety

Falsification: exactly 3 candidates (all real; chevron/text-only rows → 0).
Scrolled2: 0 raw false positives (data-driven over every text-only row).
Settings home asset: 0 candidates. Synthetic battery: real ON/OFF controls
detected; text-only row, chevron, uniform pill, partial/clipped switch, icon
square, divider, badge-with-text, knob-fills-track glyph, empty region all
rejected. Text cannot create a control (OCR = supporting structural evidence
only).

## 10. Type/state ownership split

- Python candidate type: `switch` (canonical); existing adapter mapping
  `"switch" => "toggle"` → Runtime `PerceptionType = "toggle"`. No second
  vocabulary.
- Python `switch_state` = `None` (non-authoritative).
- C# `ImageSwitchStateProvider` remains the sole switch-state authority
  (unchanged by the repair). Test oracle replicates its `ClassifySwitchRegion`
  algorithm exactly (verified line-by-line); C# provider remains production
  authority; C# targeted provider tests green.

## 11. Same-frame state proof

C#-oracle applied to PIPELINE CANDIDATE bounds on the SAME frame: master ON,
Stay awake ON, Automatic updates OFF (matches GT + independent scan). No second
screenshot for state.

## 12. Binding / StateBelief non-regression

Binding (`World/BindingAnalysis.cs`) and StateBeliefReducer
(`World/StateBeliefReducer.cs`) UNCHANGED (not in the repair commit). Fresh
candidates carry normalized [0,1] bounds + canonical type (the
`ISwitchStateReader`/Binding contract). C# targeted suite
(SwitchStateReader, RealImageClassifier, PerceptionToSemanticBinding,
StaleFrameSafety, AgentSemanticClosedLoop, ArchitectureGuard): 82/82 PASS.

## 13. Zero model / single pass / authority

- LLM calls = 0, VLM calls = 0; no DSH cognition.
- Exactly 1 YOLO pass + 1 OCR pass per frame (pass-count instrumentation;
  RPER-11). Raw-pixel detector consumes the already-decoded image only.
- Authority delta = NONE: evidence generation only — no SetSwitch
  authorization, no DesiredValue choice, no StateBelief/Binding/GoalEvidence/
  DeviceAction creation, no ADB execution.

## 14. Scenario / resolution audit

- No scenario-specific production rules (grep + RPER-12 + PER-T9): no
  DeveloperOptions/StayAwake/AutomaticSystemUpdates/Wi-Fi/Bluetooth/API/AVD/
  page-y/fixture-hash logic.
- Raw-pixel thresholds are generic Android switch track morphology in the
  canonical 720px preprocessed space, scaled by `img_w/720` — not tied to
  1080×1920 or 720×1120.
- One-candidate-per-row is a row-anchored structural search (single best
  right-side control per OCR row), not a hardcoded "one toggle per semantic
  row" invariant (recall limitation only, no false-positive risk).

## 15. Historical asset semantics reconciliation

The older record
(`perception-actionable-toggle-evidence-reality-falsification-correction.md`)
claimed the API35 Developer Options page "renders NO toggles". That
intermediate finding is contradicted by the repo-owned falsification frame
(pixel-verified teal tracks + white knobs at 4 rows; confirmed by the review's
independent scan reproducing the GT exactly). The old record referred to an
unnamed page-level inspection, not the retained fixture; the retained frame and
its GT are authoritative. No repo documentation claims THE RETAINED FILE
contains no toggles.

## 16. Deployment receipt finding (governance)

The repaired pipeline changed production identity (3 of the 14
behavior-defining modules: engine.py / heuristics.py / server.py), but the
committed deployment receipt
(`platforms/perception/governance/artifacts/current-active-identity.json`,
pipelineRevision `prev:9e31f8d6…`) exactly represents the PREVIOUS pipeline
(verified: the pre-repair tree computes the identical revision; the repaired
pipeline computes `prev:c5f50688…`). Therefore the Vision-host identity test
failures (CORR_HOST03/04/09, DI16, IdentityMatch, H6/H8/H11/H12/H14) and the
governance `test_RSI08_active_convergence` failure are classified as:

**STALE_DEPLOYMENT_RECEIPT_GOVERNANCE_FAILURE** — the receipt must be
regenerated when the pipeline changes (deployment-time governance step).

They are NOT defects of the raw-pixel detection and NOT "unrelated
pre-existing failures"; they are the direct, expected governance consequence of
the repair's pipeline change against a receipt frozen at the previous
pipeline. Receipt regeneration is NOT performed in this gate (not an explicit
canonical graduation operation here); it is recorded for the deployment flow.

## 17. Validation summary (fresh, independent, at canonical HEAD 4d33837)

- Production reality pipeline, falsification asset: PASS (3 raw candidates,
  states ON/ON/OFF, tight bounds).
- Production reality pipeline, scrolled2 asset: PASS (13 candidates, 11 raw).
- PER-REAL-01 + RPER-1..12 (real YOLO/OCR/fusion, no mocks): 13/13 PASS
  (re-run at HEAD during governance persistence).
- Python perception suites: tests/ 55/55; training 99/99; evaluation+governance
  +package 181/182 (1 environmental RSI08 — stale receipt, above).
- C# build: 0 errors. C# suite: 1043/1056 (13 Vision environmental —
  stale-deployment-receipt, above).
- `scripts/check-consistency.sh`: ALL PASS. `openspec validate --strict`: PASS.

## 18. Remaining limitations

- Raw-pixel missing-class buyer demonstrated on the Developer Options page
  family (top un-scrolled: dark ON tracks; Debugging section: light OFF
  tracks); not yet demonstrated on other Settings page families; Wi-Fi page
  larger toggle fail-closes in the raw path (no hallucination) and is covered
  by the YOLO switch + icon path.
- s05 (clipped), s14 (no text row) on scrolled2, sw4 (text-less bottom row) on
  the falsification frame are legitimately not discovered (row-anchored
  search; fail-closed by design).
- The scrolled2 groundtruth omits the master switch (y≈124); the frame contains
  15 real switches; tests unaffected; GT completeness to be corrected
  opportunistically.
- One candidate per OCR row (recall limitation for multi-control rows).
- Full scroll-toggle physical loop, popup integration, and universal Android
  control recognition are NOT claimed.

## 19. Decision

**GRADUATED** — `PERCEPTION_ACTIONABLE_TOGGLE_REALITY_REPAIR_INTEGRATED`.

Generic deterministic raw-pixel toggle candidate recovery under detector class
gaps is independently verified (positive-sensitive: real switches recovered on
durable falsification assets with tight bounds and correct same-frame states;
negative-safe: no fabricated controls across real and synthetic negatives) and
regression-protected by repo-owned reality tests. No Runtime semantics, no new
authority, no LLM/VLM, no YOLO training, no Binding/StateBelief/GoalEvidence
changes, no popup/dsh touch.

This decision was persisted in the canonical repository governance pass
(2026-08-16) after re-proving behavioral equivalence at HEAD `4d33837`
(reality tests 13/13, unit tests 23/23 re-run green; fixtures/GT/source
unchanged), followed by truthful task completion, strict OpenSpec validation,
and canonical OpenSpec archive.
