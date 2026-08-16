# Design: Perception Actionable Toggle Evidence Reality Repair

## Root Cause

### Live Falsification

On Android 15 / API35 Developer Options page:
- YOLO candidates: 34, all `text_block`
- No `icon`, `switch`, `toggle`, or empty-text candidates

The previous `apply_toggle_inference_heuristic` required an existing control-like candidate (icon, empty-text) to associate with a text row. Without such candidates, no toggle could be inferred.

### Classification

**PRIMARY**: RAW_CONTROL_CANDIDATE_GENERATION_GAP

**Components**:
- YOLO_CONTROL_CLASS_GAP: current YOLO weights do not detect controls on this page
- FUSION_DEPENDENCY_ON_PREEXISTING_CONTROL_CANDIDATE: previous heuristic had no way to discover controls from raw pixels

## Repair Approach

### Raw-Pixel Candidate Generation

The fusion layer must receive access to the already-decoded image and perform bounded raw-pixel structural search.

#### Flow

```
current image
+ OCR text rows
+ existing YOLO evidence

→ for each text row:
    → derive right-side search region
    → scan raw pixels for toggle-like structure
    → validate geometry (aspect ratio, compactness)
    → if validated: create candidate with type="switch"
```

#### Search Region

For each text row with bounds (x1, y1, x2, y2):
- Search region: x from row_x2 to screen_right - small margin
- Vertical range: row_y1 - padding to row_y2 + padding
- This is generic, not target-specific

#### Toggle-Like Structure

Evidence dimensions:
- Horizontally elongated compact region
- Aspect ratio roughly 2:1 to 3:1 (width:height)
- Rounded/pill-like outer contour (optional)
- Internal thumb-like compact region (optional)
- Local contrast against page background

#### Fail Closed

- Ambiguous shape: do not emit toggle
- Partially clipped: do not emit toggle
- Chevron/icon/badge/pill: reject based on geometry

## Authority Split

- Python Perception: toggle candidate discovery, bounds, type
- C# ImageSwitchStateProvider: ON/OFF/UNKNOWN from same screenshot pixels
- Python switch_state field: remains None, not authoritative

## Implementation Plan

### Slice 1: OpenSpec + falsification record (this document)
### Slice 2: Raw-pixel candidate generation in fusion engine
### Slice 3: Reality regression test (PER-REAL-01)
### Slice 4: Negative reality tests (RPER-3, RPER-4, RPER-5)
### Slice 5: C# integration verification
### Slice 6: Full validation
