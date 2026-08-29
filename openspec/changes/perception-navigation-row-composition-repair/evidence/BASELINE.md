# IR-G0 Perception Row Composition Baseline

Date: 2026-08-27

## Human Gate

The Human selected the perception-side repair path and authorized completion without another pause when the evidence confirms the expected conclusion: “如果是预期的结论，你就直接也干完，直到解决完问题或者需要人裁决”. This authorization does not include a Runtime contract or authority change.

## Live Capture

- Device: `p26_pixel`, Android API 35, 1080×2400 @ 420 dpi.
- Screen: Android Settings root, launched with `android.settings.SETTINGS`.
- Screenshot: `evidence/before/settings-root.png`.
- Production pipeline output: `evidence/before/settings-root-perception.json`.
- Screenshot SHA-256: `1f8c31e393c79e924467d345183274b5acd6070965de8dc8073a80155f979406`.
- Perception JSON SHA-256: `a12d964116e0b18f8b1cf331d6ba4dbd711b7868bb859b0a7f8600e58375beda`.

## First Divergence Evidence

The production pipeline returned 25 YOLO detections, 15 OCR occurrences, and 25 fused candidates. Fusion emitted:

- `Network & internet` ×3, all sharing OCR occurrences `ocr_3` + `ocr_4` but using three separate YOLO boxes (`det_12`, `det_22`, `det_17`).
- `Connected devices` ×3, all sharing OCR occurrences `ocr_5` + `ocr_7` but using three separate YOLO boxes (`det_18`, `det_14`, `det_25`).
- `Storage` ×2, sharing OCR occurrences `ocr_14` + `ocr_15` but using two separate YOLO boxes (`det_4`, `det_7`).
- `Recent apps, default apps` and `Notification history, conversations` as independent `menu_item` candidates although each is a subordinate description under a titled physical row.

Therefore the defect is not duplicate OCR text and cannot be safely repaired with global text deduplication. One physical row is being represented by title, description, and overlapping detector boxes that are each promoted at the fusion boundary.

## Boundary Baseline

Before implementation, `git diff --name-only` is empty for:

- `src/UniClaw.Runtime/`
- `src/UniClaw.Semantic.Settings/`
- `src/UniClaw.Runtime.Adapters/`

Existing dirty ValidationHarness work belongs to the active traversal-acceptance change and is preserved.
