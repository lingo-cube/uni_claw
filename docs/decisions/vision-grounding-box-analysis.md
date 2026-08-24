# Vision Grounding Box Analysis

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Task: PROJECT_LEADER_VISION_GROUNDING_BOX_CALIBRATION_ANALYSIS — analyze the
> EBD D2 OCR bounding-box offset from evidence. **Analysis only** (no Agent /
> Traversal / SourceGroundingNormalizer / fail-closed contract change; no
> XML/ADB correction of OCR coordinates).
>
> Evidence source: EBD real-device runs (`/tmp/ebd_real_evidence.txt`,
> `/tmp/ebd_obs_*.xml`) + a calibration probe that dumps, per observation, the
> raw OCR candidate bounds and the auxiliary uiautomator bounds for the same
> texts (auxiliary = analysis only).

---

## Phase 1 — Evidence

### Transform chain (code-verified)

```
AdbScreenshotSource (1080x1920)
  → screenshot JPEG → LocalVisionPerceptionSource → Python server /v1/analyze
  → preprocess: crop top/bottom 0.0625 each (top_px=120, bottom_px=120) + resize to max_width (scale≈1.6875)
  → YOLO + ROI/full OCR in PREPROCESSED space
  → fuse_evidence (preprocessed pixel space)
  → remap_coords(evidence, scale, top_px, orig_w, orig_h):  x_orig=x*scale; y_orig=y*scale+top_px; norm=…/orig
  → enforce_geometry(orig_limits) → candidates (normalized, original space)
C# side: LocalVisionPerceptionSource parses bounds as-is → ElementBounds (no transform)
tap: CoordinateMapper.ToPixelCenter(bounds, 1080, 1920) → device pixels
```

The remap math is correct for its inputs (verified in `preprocessing.py` /
`remap.py`); the C# side applies no transform.

### Calibration measurement (same observation, OCR vs uiautomator)

| frame | row | OCR center | XML center | offset |
|-------|-----|-----------|------------|--------|
| seq=1 (rest, no scroll) | networkinternet | 0.430 | 0.428 | **+0.003** |
| seq=1 | connecteddevices | 0.535 | 0.548 | −0.013 |
| seq=1 | notifications | 0.776 | 0.788 | −0.012 |
| seq=3 (after scroll) | connecteddevices | 0.394 | 0.355 | +0.039 |
| seq=5 (after scroll) | storage | 0.317 | 0.248 | +0.069 |
| seq=6 (dispatch frame) | **location** | **0.661** | **0.566** | **+0.095 (~190px)** |
| seq=6 | safetyemergency | 0.792 | 0.686 | +0.106 |
| seq=6 | accessibility | 0.437 | 0.325 | +0.112 |

- **At rest (seq=1) the OCR boxes match the uiautomator truth within ±0.02**
  (the transform chain is correct; the OCR boxes are narrow text boxes but
  correctly centered).
- **The downward offset grows with scroll activity** (≈0 → +0.04 → +0.07 →
  +0.10-0.11) — the screenshot (OCR) and the slow uiautomator dump (1-3s)
  capture DIFFERENT scroll-settle moments of the same observation.

### Tap consequence (measured)

Dispatch frame seq=6: the "location" element carries the OCR box
y=0.650-0.672 → center 0.661 → tap at 1269px. The settled layout (uiautomator)
places Location at [970,1202]px and Safety & emergency at [1202,1432]px —
**1269px lands inside Safety & emergency** (confirmed by the post-tap device
frames: "Emergency information / Emergency SOS"). The tap hit the row below.

## Phase 2 — Failure Classification

| option | verdict | evidence |
|--------|---------|----------|
| A. OCR detection box error | **NO** | boxes fit their own frame; rest-frame centers match truth within ±0.02 |
| B. Coordinate transform error | **NO** | server `preprocess`→`remap_coords` math verified; rest-frame match proves the chain |
| **C. Frame normalization error (coordinate freshness)** | **YES** | the coordinate is normalized to a screenshot frame captured mid-scroll-settle; the tap executes after the list settled/bounced → the coordinate is stale to the execution-time frame; offset grows with scroll depth (temporal signature) |
| D. Semantic candidate association error | NO | the "location" candidate correctly associates the Location row (its box is the OCR box) |
| E. Test fixture issue | NO (contributing) | the fixture does not create the offset; large adaptive scrolls (0.4+) maximize the settle drift |

## Phase 3 — Ownership

```
AuthorityDelta:   NONE
ArchitectureDelta: NONE
```

Unaffected: Agent authority, Traversal ownership, Semantic Capability boundary,
Vision-first contract, fail-closed contract. (Analysis only — no production
change.)

## Phase 4 — Analysis Directions

1. **OCR bbox 来自原始 frame 坐标？** — No: YOLO/OCR run on the preprocessed
   (cropped+resized) frame; `remap_coords` maps back to the original
   full-screen normalized space. The remap is correct (rest-frame match).
2. **resize/crop/scale/viewport transform 导致偏移？** — The chain exists but is
   NOT the error source: at rest the post-remap coordinates match the
   uiautomator truth within ±0.02; the offset appears only after scrolls and
   grows with depth → temporal frame drift, not transform math.
3. **Semantic candidate ↔ OCR box 错误关联？** — No: the "location" candidate
   is the OCR box of the Location text; association is correct.
4. **多个同名文本错误匹配？** — No same-text mismatch for the dispatch target.
   (The location-pin phantom "LoO"/"Lo"/"Lou" is a different-text detection,
   unrelated to the box offset.)

## Root Cause (D2 refined)

**The tap coordinate is normalized to a screenshot frame captured while the
Settings list was still settling (over-scrolled/bouncing after the last
exploration scroll); by tap time the list settled to a different layout, so the
stale coordinate hits the row below.** The OCR boxes are correct for their own
frame; the frame is not the execution-time frame. This is an observation
freshness / frame-settle consistency issue in the exploration→dispatch flow,
not a detection or transform defect.

## Recommended Direction (owner: Agent exploration/settle policy — NOT performed here)

- The Runtime's post-scroll evidence-quality settle validates bounds validity
  but not scroll STABILITY; a scroll-stability (no-motion) acceptance, or a
  re-observe immediately before semantic dispatch, would keep the tap
  coordinate fresh to the execution-time frame. (Deterministic scope check
  required; must not weaken fail-closed or add scenario knowledge.)
- Alternative: dispatch re-resolves the target's fresh bounds from a
  post-decision observation before lowering the tap (same frame discipline as
  the branch-grounding gate).

## Remaining Uncertainty

- Whether the drift is overscroll-bounce (list at bottom edge) vs ongoing fling
  momentum: both produce the observed temporal signature; distinguishing needs
  a slow-motion frame series. The fix direction (fresh-frame-at-dispatch) covers
  both.
- The deterministic worlds have no settle physics, so no deterministic test can
  reproduce this; validation requires the real device.
