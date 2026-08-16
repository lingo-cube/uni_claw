# Reality fixtures — Developer Options toggle repair

Repo-owned reality evidence for change
`perception-actionable-toggle-evidence-reality-repair` (§21 of the gate
protocol: durable, repo-tracked reality assets + reproduction metadata).

## Assets

| File | Description |
|------|-------------|
| `developer-options-falsification.png` | Live falsification frame: Android 15 Developer Options page (top, un-scrolled), 1080×1920. YOLO emits 34 `text_block` detections and ZERO control-class detections; the page contains 4 real switches. Pre-repair production pipeline returned 0 switch candidates. |
| `developer-options-falsification.groundtruth.json` | Verified ground truth: 4 switches with independent pixel-verified bounds/states. |
| `developer-options-scrolled2.png` | Same page scrolled to the Debugging section: 14 real switch rows (12 with text rows, 1 clipped by its row band, 1 with no text row). |
| `developer-options-scrolled2.groundtruth.json` | Verified ground truth: 14 switches, two independent state-verification methods. |

## Capture metadata

- Device: `emulator-5554` — Android 15 / API 35 (Google APIs, arm64-v8a).
- Capture: `adb exec-out screencap -p` (full-screen, no scaling).
- `developer-options-falsification.png`: captured 2026-08-15T13:21:00Z.
- `developer-options-scrolled2.png`: captured 2026-08-15T13:24:00Z (scrolled
  to the Debugging section).

## SHA-256

```
7e74c9683e9cf068fb235aa3eecf2e2529f7d5e819b34ee77d380c951c3d6ece  developer-options-falsification.png
9892950199d405b30938cd25bb55f8cdcaae83afb30283e9d9c3a4b5af33f51a  developer-options-scrolled2.png
a5907449d3a6360f3ff242d35adc05c3a833a5d458cbec52e87b33897ddcb0e8  developer-options-falsification.groundtruth.json
c6850f5be0efc8d230dc4628625a5e7783dcd5f7174a7471beb8e70bdb346dbc  developer-options-scrolled2.groundtruth.json
```

## Reproduction evidence

Pre-repair production pipeline on the falsification frame
(`_run_pipeline`, unmodified code):

```
yolo: text_block x34, icon 0, switch 0, toggle 0, checkbox 0
switch candidates: 0   (4 real switches on screen, none discovered)
```

Post-repair production pipeline on the same frame:

```
[992, 240, 1044, 273]  raw_pixel_toggle  ON   (master 'Use developer options', GT x992-1044 y241-273)
[1010, 634, 1066, 670] raw_pixel_toggle  ON   ('Stay awake', GT x1012-1063 y636-667)
[1010, 1398, 1065, 1434] raw_pixel_toggle OFF  ('Automatic system updates', GT x1012-1063 y1400-1431)
```

The 4th real switch (bottom Debugging-section row, ON) has no OCR text row,
so the row-anchored search legitimately cannot reach it (fail closed — no
candidate emitted, documented in the ground truth).

Post-repair pipeline on `developer-options-scrolled2.png`: 12 of the 14
ground-truth tracks discovered with tight bounds (one candidate per row, no
duplicates, no raw-path false positives); the y650 track is clipped by its
OCR row band (fail closed) and the y1797 track has no text row (fail
closed).

## Ground-truth verification methodology

Ground truth was produced by INDEPENDENT whole-image structural analysis of
the raw frames — NOT by the fusion pipeline, YOLO, or OCR:

1. Background-relative luminance mask (per-row median of the left 900px as
   background; |lum − bg| > 8).
2. 4-connected component labeling.
3. Track geometry filters: width 25–140, height 12–60, aspect 1.2–4.5,
   x1 ≥ 0.62·width.
4. Each state confirmed by TWO methods on the same frame:
   - track median luminance + dark/light knob column position (mass
     centroid of |lum − median| > 25 outliers over the interior), and
   - the exact C# `ImageSwitchStateProvider` algorithm (middle-third band
     baseline median, |lum − baseline| ≥ 60 outlier pixels per left/right
     half, difference = rightRatio − leftRatio, > +0.15 ON / < −0.15 OFF).

Both methods agree on every row of both frames.

## Consumers

- `tests/test_reality_repair.py` — PER-REAL-01 + RPER-1..RPER-12 run the
  production pipeline against these fixtures (real YOLO + OCR + fusion).
