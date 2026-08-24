# PROJECT_LEADER_VISION_GROUNDING_BOX_CALIBRATION_ANALYSIS_RESULT

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Task: PROJECT_LEADER_VISION_GROUNDING_BOX_CALIBRATION_ANALYSIS — analyze the
> EBD D2 OCR bounding-box offset from evidence. Analysis only; no production
> change; no XML/ADB correction of OCR coordinates.
>
> **AuthorityDelta: NONE — ArchitectureDelta: NONE.**

---

## 1. Evidence Summary

E4 evidence (EBD real-device runs + a per-observation calibration probe):

- **Transform chain verified** (`LocalVisionPerceptionSource` → Python server):
  screenshot → `preprocess` (crop 0.0625 top/bottom, resize to max_width) →
  YOLO/OCR in processed space → `remap_coords(scale, top_px, orig_w, orig_h)`
  back to original normalized space → C# parses as-is (no transform). The
  remap math is correct.
- **Rest-frame calibration** (seq=1, no scroll): OCR box centers match the
  uiautomator truth within **±0.02** for every row (networkinternet +0.003,
  connecteddevices −0.013, notifications −0.012, battery +0.02) — the chain is
  accurate at rest; OCR boxes are narrow text boxes but correctly centered.
- **Post-scroll drift**: the downward offset grows with scroll depth
  (seq=3 +0.04, seq=5 +0.07, seq=6 **+0.10-0.11** ≈ 190-210px). The screenshot
  (OCR) and the slow uiautomator dump (1-3s) capture different settle moments
  of the same observation.
- **Tap consequence**: dispatch-frame "location" OCR box center 0.661 →
  1269px; the settled layout places Location at [970,1202]px and Safety &
  emergency at [1202,1432]px → the tap lands on Safety & emergency (confirmed
  by post-tap device frames).

## 2. Failure Classification

**C — Frame normalization error (coordinate freshness)**. The tap coordinate is
normalized to a screenshot frame captured mid-scroll-settle; the tap executes
after the list settled/bounced → the coordinate is stale to the execution-time
frame. Ruled out with evidence: **A** (detection box — boxes fit their own
frame, rest-match ±0.02), **B** (coordinate transform — remap verified, rest
state proves it), **D** (candidate association — "location" correctly
associates the Location row), **E** (fixture — does not create the offset;
large adaptive scrolls amplify the settle drift).

## 3. Root Cause

The exploration's last scroll leaves the Settings list settling (over-scrolled /
bouncing at the list edge) when the dispatch-frame screenshot is taken; the tap
coordinate comes from that stale frame, and by tap time the list settled to a
different layout — the tap hits the row below (Safety & emergency). The OCR
boxes are correct for their own frame; the frame is not the execution-time
frame.

## 4. Owner

**Agent — exploration/dispatch frame-settle policy** (the post-scroll
evidence-quality settle validates bounds validity, not scroll stability; the
dispatch uses the current frame's bounds without a fresh re-observe). The
perception layer is NOT at fault (boxes accurate at rest); the transform layer
is NOT at fault (remap verified). Owner judgment per the EBD D2 escalation,
now narrowed by this analysis.

## 5. Recommended Fix (NOT performed — analysis only; requires scope check)

1. **Scroll-stability acceptance** in the exploration settle: accept a
   post-scroll frame only when the screen has stopped moving (e.g., two
   consecutive observations with equal scroll-position evidence), so the
   accepted frame is the settled frame.
2. **Fresh-bounds re-resolve before semantic dispatch**: after the dispatch
   decision, re-observe (bounded) and re-resolve the target's bounds from the
   freshest same-Container frame before lowering the tap — the same
   fresh-evidence discipline as the branch-grounding gate.
   Both directions must: stay bounded, preserve fail-closed, add no scenario
   knowledge, and not touch Traversal/Normalizer/Agent authority boundaries.

## 6. Architecture Impact

| dimension | impact |
|-----------|--------|
| Agent authority | NONE (analysis only; the recommended fix stays inside the Agent's existing settle/dispatch seam) |
| Traversal | NONE |
| SourceGroundingNormalizer | NONE (standards untouched) |
| Semantic Capability boundary | NONE |
| Vision-first / fail-closed contracts | NONE |

**ArchitectureDelta: NONE** (no production change in this task).

## 7. Remaining Uncertainty

- **Overscroll-bounce vs ongoing-fling momentum**: both produce the observed
  temporal signature (offset grows with scroll depth); distinguishing needs a
  slow-motion frame series. The recommended directions cover both.
- **Deterministic reproduction**: the deterministic worlds have no settle
  physics, so no fake-world test can reproduce the drift; validation must be
  real-device. Deterministic coverage of the fix would need a settle-physics
  fixture (evidence-model addition, out of scope here).
- The tap target tolerance varies by screen density (Capstone's large fixture
  rows tolerate the drift; the dense Settings list does not) — the fix should
  be validated on the Settings screen specifically.
