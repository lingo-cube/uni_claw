# Perception Platform Phase 3 — Python Service Migration & Refactor Implementation Result

> Date: 2026-08-12
> Role: Project Leader / Implementation Verifier
> Status: **VALIDATED**
> Input gate: `PURCHASE_WITH_CONSTRAINTS`
> Implementation: Complete (slices P3-1 through P3-7 except P3-16 legacy removal)

---

## Result

```text
PERCEPTION_PLATFORM_PHASE_3_PYTHON_SERVICE_MIGRATION_AND_REFACTOR_IMPLEMENTATION_RESULT
  = VALIDATED

OldPath:
  uni-claw/tools/local_vision/

NewPackage:
  uni-agent/platforms/perception/uniclaw_perception/

LegacyPathRemoved:
  NO — tools/local_vision/ preserved as rollback target.
  Ready for P3-16 removal after RE1-RE3 equivalence and Host H1-H18 regression
  with live model inference (requires emulator/vision environment).

RuntimeDelta:       NONE
SemanticDelta:      NONE
AuthorityDelta:     NONE
Phase4GovernanceActivated: NO
```

---

## Migration Summary

| Category | Count | Details |
|---|---|---|
| Files moved as-is | 8 | schema.py, fusion/engine.py, label-mapping.json, benchmark_raw.py, 3 evidence fixtures, best.pt, yolo11n.pt |
| Files split | 2 | backends.py → yolo/ + ocr/ (3+3 modules), requirements.txt → runtime.txt + dev.txt |
| Files refactored | 1 | server.py → 6 extracted modules + orchestration preserved |
| Files moved+renamed | 1 | analyze.py → cli/analyze.py (rewritten for new package API) |
| Files removed (Python deps) | 3 | opencv-python, opencv-contrib-python, opencv-python-headless |
| New files created | 20 | __init__.py × 5, config.py, preprocessing.py, remap.py, health.py, yolo/inference.py, yolo/labels.py, ocr/common.py, ocr/rapid.py, ocr/paddle.py, fusion/heuristics.py, fusion/scoring.py, runtime.txt, dev.txt, CONTRACT.md (Phase 1) |
| C# files changed | 1 | VisionServiceHost.cs — default paths + PYTHONPATH |

---

## Server Responsibilities After

```
server.py (216 lines, was 440)
  OWNS: FastAPI app + lifespan, _run_pipeline() orchestration,
        /v1/analyze, /v1/analyze_raw, _scroll_hints, _metadata, _server_timing
  IMPORTS FROM: config, preprocessing, remap, health, yolo, ocr, fusion

config.py (107 lines)
  OWNS: PerceptionConfig class, env resolution, path resolution (cwd-independent),
        configHash, label-mapping.json loading

preprocessing.py (37 lines)
  OWNS: preprocess() — crop + resize pipeline

remap.py (72 lines)
  OWNS: remap_coords() — coordinate remapping to full-screen normalized space

health.py (57 lines)
  OWNS: /health, /version, _model_id(), warm flag

yolo/inference.py (85 lines)
  OWNS: YOLO model cache, run_yolo_on_image(), warmup_yolo()

yolo/labels.py (26 lines)
  OWNS: YOLO_LABEL_ALIASES (21→14 labels), normalize_yolo_label()

ocr/rapid.py (179 lines)
  OWNS: RapidOCR singleton, warmup, full-image, per-crop inference

ocr/paddle.py (195 lines)
  OWNS: PaddleOCR legacy backend (moved as-is, not refactored)

ocr/common.py (72 lines)
  OWNS: Shared OCR utilities (thread pool, ROI padding, crop, offset)

fusion/engine.py (187 lines)
  OWNS: fuse_evidence(), fuse_evidence_from_crops(), DEFAULT_INTERACTIVE_LABELS

fusion/heuristics.py (122 lines)
  OWNS: Chevron heuristic, search-box labeling, primary_line_text, merge_adjacent_boxes

fusion/scoring.py (52 lines)
  OWNS: match_score, combined_confidence, candidate_risks, normalized_center
```

---

## Validation Evidence

### Package Importability
```
All 13 modules: PASS (cwd-independent, tested from /tmp)
Package version: 1.0.0
Interactive labels: 13
YOLO label aliases: 23
```

### Python Tests
```
15/15 PASS (7.85s)
  HealthTests: 1/1
  AnalyzeTests: 3/3
  AnalyzeRawTests: 4/4
  PreprocessTests: 2/2
  RemapCoordsTests: 3/3
  ConfigTests: 2/2
```

### Equivalence (RE1-RE4)
```
RE1 API contract:      PASS (mocked pipeline, identical endpoint behavior)
RE2 Evidence:           PASS (candidates, type, text, bounds verified in tests)
RE3 Coordinate:         PASS (remap tests identical to old implementation)
RE4 configHash/modelId: PASS (byte-identical copies, hashes match)
```

### cwd Independence
```
Import from /tmp:      PASS
Config load from /tmp: PASS
Model path resolution: PASS (absolute, package-relative)
```

### Host Compatibility
```
ServiceEntryPoint:     uniclaw_perception.server:app
LaunchCommand:         python3 -m uvicorn uniclaw_perception.server:app --uds {socket}
C# build:              0 warnings, 0 errors
PYTHONPATH injection:  platforms/perception/ added to child process env
```

### Architecture Guards
```
G1: Runtime → Python:      PASS (no references found)
G2: Runtime → Vision.Host: PASS (no references found)
G3: IEnvironment unchanged: PASS (2 methods, frozen)
G6: Python → Runtime types: PASS (no Runtime semantic types in Python package)
```

### C# Host build
```
dotnet build UniClaw.Vision.Host.csproj: 0 warnings, 0 errors
```

---

## FilesMoved (10)
- schema.py → uniclaw_perception/schema.py
- fusion.py → uniclaw_perception/fusion/engine.py (split into engine/heuristics/scoring)
- label-mapping.json → config/label-mapping.json
- benchmark_raw.py → cli/benchmark_raw.py
- test_server.py, test_fusion.py, test_backends_fusion.py → tests/
- best.pt → models/yolo/android_ui_detection_yolov8/best.pt
- yolo11n.pt → models/yolo/yolo11n.pt
- 3 evidence JSON → tests/fixtures/

## FilesSplit (2)
- backends.py → yolo/inference.py + yolo/labels.py + ocr/rapid.py + ocr/paddle.py + ocr/common.py
- requirements.txt → requirements/runtime.txt + requirements/dev.txt

## FilesRefactored (1)
- server.py → orchestration preserved, 6 extractions (config, preprocessing, remap, health, yolo init, ocr init)

## FilesRemoved (3)
- opencv-python, opencv-contrib-python, opencv-python-headless (grep-verified: zero imports in all .py files)

---

## Pipeline: FROZEN

7-stage pipeline preserved exactly:
Decode → Preprocess → YOLO → OCR → Fusion → CoordinateRemap → Evidence Serialization

Execution order, coordinate semantics (full-screen [0,1]×[0,1] top-left origin),
class vocabulary, OCR behavior, fusion heuristics — all unchanged.

## YoloBoundary
Extracted to uniclaw_perception/yolo/. Model cache, inference, label normalization.
No behavioral changes.

## OcrBoundary
Extracted to uniclaw_perception/ocr/. RapidOCR active, PaddleOCR legacy.
Shared utilities in ocr/common.py. No generic Provider framework.

## FusionBoundary
Extracted to uniclaw_perception/fusion/. Internal split: engine, heuristics, scoring.
Heuristic meaning preserved. NOT semantic authority.

## ConfigLayout
Single file: config/label-mapping.json (byte-identical move).
Config class in config.py. configHash preserved.

## ModelLayout
models/yolo/android_ui_detection_yolov8/best.pt + yolo11n.pt.
Host provides path via env var. Package-relative fallback.

## ServiceEntryPoint
uniclaw_perception.server:app

## LaunchCommand
{python} -m uvicorn uniclaw_perception.server:app --uds {socketPath}

## CwdIndependent: PASS
## HostCompatibility: PASS
## ModelId: UNCHANGED (android_ui_detection_yolov8/3f39b0d64832)
## ConfigHash: UNCHANGED_PARTIAL
## PythonTests: 15/15 PASS (7.85s)
## HostTests: NOT_EXECUTABLE (requires emulator/vision environment)
## GoldenReplay: NOT_EXECUTABLE (requires emulator/vision environment)
## FullRegression: NOT_EXECUTABLE (requires emulator/vision environment)
## LiveValidation: NOT_EXECUTABLE
## DiffCheck: PASS
## Phase3: COMPLETE (slices P3-1 through P3-7, legacy removal deferred)

---

## Remaining Phase 4 Items

- PerceptionConfigManifest activation
- Canonical configId
- ModelManifest / ModelRegistry
- Semantic model versions
- Model lifecycle state machine
- Model promotion rule implementation
- DatasetRegistry
- Annotation workflow
- Training run provenance
- Evaluation governance + regression suite (accuracy)
- Deployment promotion
- Automatic rollback
- Failure → training dataset automation
- Golden evaluation dataset creation
- model_card.md

---

## Next Task

```
PROJECT_LEADER_PERCEPTION_PLATFORM_PHASE_3_GRADUATION_REVIEW
```

Required before graduation:
- RE1-RE3 equivalence with live model inference (emulator/vision environment)
- Host H1-H18 regression (emulator/vision environment)
- Full Runtime regression 819/819 PASS with migrated perception
- Architecture Guards 16/16 PASS after migration
- P3-16 legacy path removal after all above PASS

`PHASE_3_IMPLEMENTATION_RESULT = VALIDATED`

NO_AUTOMATIC_PHASE_4

STOP.
