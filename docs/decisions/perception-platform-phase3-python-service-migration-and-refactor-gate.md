# Perception Platform Phase 3 — Python Service Migration & Refactor Gate

> Date: 2026-08-12
> Role: Project Leader / Architecture & Migration Gate
> Mode: `ARCHITECTURE_AND_MIGRATION_GATE`
> Inputs:
> - Phase 1: Contract Extraction — VALIDATED
> - Phase 2: Provider Host — GRADUATED
> - Operational Governance Audit — complete
> - Current implementation: `uni-claw/tools/local_vision/` (authoritative)
> Result: `PERCEPTION_PLATFORM_PHASE_3_PYTHON_SERVICE_MIGRATION_AND_REFACTOR_GATE_RESULT`
> Decision: **PURCHASE_WITH_CONSTRAINTS**
> Implementation authority: **NOT YET GRANTED** (requires separate task authorization)

---

## 0. Gate result

```text
PHASE_3_PYTHON_SERVICE_MIGRATION_AND_REFACTOR_GATE
  = PURCHASE_WITH_CONSTRAINTS

ARCHITECTURE_PRESSURE
  = NONE

MIGRATION_FEASIBILITY
  = CONFIRMED

RUNTIME_DELTA
  = NONE

SEMANTIC_DELTA
  = NONE

AUTHORITY_DELTA
  = NONE
```

Phase 3 is architecturally ready. The migration is a structural move +
bounded refactor of a well-audited codebase. No behavioral changes.
No semantic authority expansion. No Phase 4 governance activation.

---

## P3-G1 — CURRENT CODE INVENTORY

### G1.1 File-level migration classification

Every file in `tools/local_vision/` and related artifacts classified with
repository evidence.

```
#  │ CURRENT PATH                                          │ CLASSIFICATION       │ EVIDENCE / PRESSURE
───┼───────────────────────────────────────────────────────┼──────────────────────┼──────────────────────────────────────────
 1 │ tools/local_vision/server.py                          │ REFACTOR             │ Owns HTTP API + config load + YOLO/OCR
   │                                                       │                      │ init + pipeline + remap + health + version.
   │                                                       │                      │ Split: config loading → config.py,
   │                                                       │                      │ model/OCR init → service initialization.
   │                                                       │                      │ Keep: HTTP API + pipeline orchestration.
 2 │ tools/local_vision/backends.py                        │ SPLIT                │ 673 lines. Contains 3 concerns:
   │                                                       │                      │ a) YOLO inference (lines 40-143)
   │                                                       │                      │ b) PaddleOCR (lines 146-493, legacy)
   │                                                       │                      │ c) RapidOCR (lines 496-673, active)
   │                                                       │                      │ Split: yolo_inference.py, ocr_rapid.py,
   │                                                       │                      │ ocr_paddle.py (legacy, keep for comparison)
 3 │ tools/local_vision/fusion.py                          │ MOVE_AS_IS           │ Single responsibility: spatial fusion.
   │                                                       │                      │ 399 lines. Self-contained. Imported by
   │                                                       │                      │ server.py and analyze.py.
   │                                                       │                      │ No split required for Phase 3.
 4 │ tools/local_vision/schema.py                          │ MOVE_AS_IS           │ Pure data classes: Box, Detection, OcrToken.
   │                                                       │                      │ 95 lines. No dependencies. No changes needed.
 5 │ tools/local_vision/analyze.py                         │ MOVE_AND_RENAME      │ CLI entry point. Separate from service.
   │                                                       │                      │ Rename: cli.py. Keep: all existing args.
 6 │ tools/local_vision/label-mapping.json                 │ MOVE_AS_IS           │ Single source of truth. Phase 4 will be the
   │                                                       │                      │ canonical config location. Do NOT split yet.
 7 │ tools/local_vision/requirements.txt                   │ SPLIT                │ 14 packages mixed: runtime + legacy + dev.
   │                                                       │                      │ Split: requirements-runtime.txt,
   │                                                       │                      │ requirements-dev.txt.
   │                                                       │                      │ Keep legacy paddleocr pins for comparison.
 8 │ tools/local_vision/benchmark_raw.py                   │ MOVE_AS_IS           │ Latency benchmark. Evaluation tool. Move to
   │                                                       │                      │ evaluation/ directory.
 9 │ tools/local_vision/__init__.py                        │ MOVE_AS_IS           │ Empty package marker.
10 │ tools/local_vision/tests/test_server.py               │ MOVE_AS_IS           │ 408 lines. 5 test classes. Validates /health,
   │                                                       │                      │ /v1/analyze, /v1/analyze_raw, _preprocess,
   │                                                       │                      │ _remap_coords, config loading.
11 │ tools/local_vision/tests/test_fusion.py               │ MOVE_AS_IS           │ Fusion logic tests.
12 │ tools/local_vision/tests/test_backends_fusion.py      │ MOVE_AS_IS           │ Backend integration tests.
13 │ tools/local_vision/tests/__init__.py                  │ MOVE_AS_IS           │ Empty package marker.

─── MODEL ARTIFACTS ───
14 │ artifacts/local-vision/models/                        │ MOVE_AS_IS           │ Move to platforms/perception/models/.
   │   android_ui_detection_yolov8/best.pt                 │                      │ Production YOLO model (6.2 MB).
15 │ artifacts/local-vision/models/yolo11n.pt              │ KEEP_TEMPORARILY     │ Legacy model. Not used in production.
   │                                                       │                      │ Keep for comparison. Deprecate in Phase 4.

─── REALITY / EVIDENCE ASSETS ───
16 │ artifacts/local-vision/                               │ MOVE_AS_IS           │ 3 evidence JSON files. Move to
   │   vision_test_controlled_screen.evidence.json         │                      │ platforms/perception/tests/fixtures/.
   │   vision_test_controlled_screen.android-ui-yolo.      │                      │ Used for regression comparison.
   │     evidence.json                                     │                      │
   │   settings-real.android-ui-yolo.evidence.json         │                      │
17 │ artifacts/assets/screenshots/                         │ KEEP_TEMPORARILY     │ Reality screenshots. Harness-owned.
   │   settings-home-api35-full-20260803.png               │                      │ Eventually move to shared reality corpus.
   │   settings-diag-20260803.png                          │                      │ Do NOT move into perception platform —
   │   manifest.json                                       │                      │ these are Harness assets consumed by
   │                                                       │                      │ perception, not owned by perception.

─── PHASE 1/2 ARTIFACTS (already created) ───
18 │ platforms/perception/CONTRACT.md                      │ KEEP                 │ Phase 1. Already in target location.
19 │ uni-agent/src/UniClaw.Vision.Host/                    │ KEEP                 │ Phase 2. Unchanged by Phase 3.

─── NOTHING ELSE MOVES ───
```

### G1.2 Summary counts

| Classification | Count | Files |
|---|---|---|
| MOVE_AS_IS | 10 | fusion.py, schema.py, label-mapping.json, benchmark_raw.py, __init__.py, 4 test files, model dir, evidence fixtures |
| SPLIT | 2 | backends.py (3-way), requirements.txt (2-way) |
| REFACTOR | 1 | server.py (config extraction) |
| MOVE_AND_RENAME | 1 | analyze.py → cli.py |
| KEEP_TEMPORARILY | 2 | yolo11n.pt, screenshots |
| KEEP | 2 | CONTRACT.md, Vision.Host |

---

## P3-G2 — TARGET PLATFORM LOCATION

### G2.1 Repository decision

```text
Target repository: uni-agent/  (same repo as VisionServiceHost)

Rationale:
  • VisionServiceHost (C#, Phase 2) lives in uni-agent/src/UniClaw.Vision.Host/.
  • Host launches the Python service. Service and Host should be in the same
    repository for atomic versioning and deployment.
  • uni-claw/ is the legacy monorepo. uni-agent/ is the graduated Runtime +
    infrastructure repository.
  • CONTRACT.md (Phase 1) already lives in uni-agent/platforms/perception/.
```

### G2.2 Exact target tree

```text
uni-agent/platforms/perception/

  CONTRACT.md                           # Phase 1 — Vision Service API contract

  uniclaw_perception/                   # Python production package
    __init__.py                         # Package marker + version
    server.py                           # FastAPI app, pipeline orchestration
    config.py                           # [EXTRACTED] Config loading, env resolution
    preprocessing.py                    # [EXTRACTED] Image crop/resize pipeline
    remap.py                            # [EXTRACTED] Coordinate remapping
    health.py                           # [EXTRACTED] Health + version endpoints
    schema.py                           # Data classes: Box, Detection, OcrToken

    yolo/                               # YOLO inference module
      __init__.py
      inference.py                      # Model loading, predict, label normalization
      labels.py                         # YOLO_LABEL_ALIASES (extracted from backends.py)

    ocr/                                # OCR inference module
      __init__.py
      rapid.py                          # RapidOCR: singleton, warmup, full-image, per-crop
      paddle.py                         # PaddleOCR: legacy backend (moved, not refactored)
      common.py                         # Shared: crop, offset, thread pool, padding

    fusion/                             # Fusion module
      __init__.py
      engine.py                         # fuse_evidence, fuse_evidence_from_crops
      heuristics.py                     # Chevron, search-box, primary-line-text
      scoring.py                        # Match scoring, confidence combination

  config/                               # Configuration (single source of truth)
    label-mapping.json                  # Label mappings + spatial params + detection conf

  models/                               # Model artifacts
    yolo/
      android_ui_detection_yolov8/
        best.pt                         # Production model (6.2 MB)
      yolo11n.pt                        # Legacy comparison model

  cli/                                  # CLI tools (NOT production service)
    analyze.py                          # Renamed from tools/local_vision/analyze.py
    benchmark_raw.py                    # Latency benchmark

  tests/                                # Python tests
    __init__.py
    test_server.py                      # HTTP API tests
    test_fusion.py                      # Fusion logic tests
    test_backends_fusion.py             # Backend integration tests
    test_config.py                      # [NEW] Config loading + env resolution tests
    test_preprocessing.py               # [NEW] Crop/resize pipeline tests
    test_remap.py                       # [NEW] Coordinate remapping tests
    fixtures/                           # Test fixtures
      vision_test_controlled_screen.evidence.json
      vision_test_controlled_screen.android-ui-yolo.evidence.json
      settings-real.android-ui-yolo.evidence.json

  requirements/
    runtime.txt                         # Production-only dependencies
    dev.txt                             # Test + benchmark dependencies

  # ── NOT in Phase 3 ──
  # training/                           # Phase 4
  # datasets/                           # Phase 4
  # evaluation/                         # Phase 4 (regression suite)
  # model_card.md                       # Phase 4
  # EffectiveConfigManifest             # Phase 4
```

### G2.3 Design rationale

Each top-level directory corresponds to a real responsibility:

| Directory | Responsibility | Evidence of need |
|---|---|---|
| `uniclaw_perception/` | Production Python package. Importable. Launchable. | server.py is the production service. Must be a proper package. |
| `uniclaw_perception/yolo/` | YOLO model loading + inference + label normalization. | Currently 100+ lines in backends.py. Extracted for testability and ownership clarity. |
| `uniclaw_perception/ocr/` | OCR backends (RapidOCR active, PaddleOCR legacy). | Currently 500+ lines in backends.py. Two backends with different lifecycles. |
| `uniclaw_perception/fusion/` | Spatial fusion of YOLO + OCR → candidates. | Already a separate file (fusion.py). Three internal concerns identified (engine, heuristics, scoring). |
| `config/` | Single source of truth for label mapping + spatial params. | Currently label-mapping.json. Phase 4 will add more config files here. |
| `models/` | Immutable model artifacts. | Currently in artifacts/local-vision/models/. |
| `cli/` | Developer tools. NOT production. | analyze.py and benchmark_raw.py are CLI tools, not service code. Separated to keep production package clean. |
| `tests/` | Python test suite. | Currently in tools/local_vision/tests/. |

Directories NOT created because they lack current responsibility pressure:
- `training/` — no training code exists
- `datasets/` — no datasets exist
- `evaluation/` — only benchmark_raw.py (moved to cli/)

---

## P3-G3 — PYTHON PACKAGE BOUNDARY

```text
TargetPackage:
  Name:            uniclaw_perception
  Entry point:     uniclaw_perception.server:app  (FastAPI ASGI application)
  Import style:    absolute imports within package
                   (e.g., from uniclaw_perception.yolo.inference import run_yolo_on_image)
  Python version:  3.11 (frozen — Intel macOS compatibility)

Working-directory independence:
  Current problem:
    server.py resolves model/config paths relative to os.getcwd():
      _MODEL_PATH = "artifacts/local-vision/models/.../best.pt"
      label-mapping.json path = "tools/local_vision/label-mapping.json"

  Phase 3 resolution:
    • Service accepts explicit paths via environment variables:
      UNICLAW_YOLO_MODEL       — absolute or Host-relative path
      UNICLAW_LABEL_MAPPING    — absolute or Host-relative path
    • If env vars are set (Host always sets them): use explicit paths.
    • If env vars are NOT set (dev CLI usage): resolve relative to
      the config/ and models/ directories within the package tree.
    • Fallback: use pathlib.Path(__file__).parent resolution to locate
      config/ and models/ relative to the package installation.
    • cwd MUST NOT be the default resolution mechanism.

Resource lookup rules (priority order):
  1. Environment variable (explicit Host/Deployment override)
  2. Package-relative path (config/ or models/ relative to uniclaw_perception/)
  3. Fail with clear error — never fall back to cwd-relative.

Import boundaries:
  • Production service (server.py) imports from: yolo, ocr, fusion, config module.
  • yolo module imports: ultralytics (external), schema.
  • ocr module imports: rapidocr_onnxruntime (external), numpy, PIL, schema.
  • fusion module imports: schema only (no external ML deps).
  • CLI tools import from: uniclaw_perception (the production package).
  • Tests import from: uniclaw_perception (the production package).
  • NO reverse imports: fusion does NOT import yolo or ocr.
  • NO circular imports: dependency graph is acyclic.

Package version:
  uniclaw_perception/__init__.py:
    __version__ = "1.0.0"
    # Version is the service/pipeline version, not model version.
    # Reported in GET /version serviceVersion.
```

---

## P3-G4 — SERVER.PY BOUNDED REFACTOR

### G4.1 Current responsibility audit

```text
server.py currently owns (lines 1-440):

  RESPONSIBILITY                  │ LINES   │ KEEP IN server.py? │ MOVE TO
  ────────────────────────────────┼─────────┼────────────────────┼──────────────
  OMP_NUM_THREADS env set         │ 6-8     │ YES                 │ (stays — import-time)
  Imports + constants             │ 9-66    │ PARTIAL             │ Constants → config.py
  _MODEL_PATH resolution          │ 42      │ NO                  │ config.py
  _OCR_LANG / _OCR_BACKEND        │ 48-51   │ NO                  │ config.py
  _OCR_TEXT_SCORE                 │ 53      │ NO                  │ config.py
  _IMAGE_SIZE                     │ 54      │ NO                  │ config.py
  _WARMUP_IMAGE                   │ 55      │ NO                  │ yolo/inference.py (warmup)
  _SPATIAL / _DETECTION_CONF      │ 57-58   │ NO                  │ config.py
  _OCR_MODE                       │ 60      │ NO                  │ config.py
  _MAX_WIDTH / _CROP_TOP / etc.   │ 62-65   │ NO                  │ config.py
  _CONFIG_HASH                    │ 65      │ NO                  │ config.py
  _WARM                           │ 66      │ YES                 │ (stays — health state)
  _TEXT_LIKELY_LABELS             │ 68      │ NO                  │ fusion/engine.py or config
  _load_spatial()                 │ 71-89   │ NO                  │ config.py
  warmup_yolo()                   │ 92-96   │ NO                  │ yolo/inference.py
  lifespan()                      │ 99-109  │ YES                 │ (stays — FastAPI lifespan)
  app = FastAPI(lifespan=lifespan)│ 112     │ YES                 │ (stays)
  _logger                         │ 114     │ YES                 │ (stays)
  unhandled_exception_handler     │ 117-126 │ YES                 │ (stays)
  _preprocess()                   │ 129-157 │ NO                  │ preprocessing.py
  _remap_coords()                 │ 160-234 │ NO                  │ remap.py
  _merge_adjacent_boxes()         │ 236-260 │ NO                  │ fusion/engine.py
  _run_pipeline()                 │ 263-339 │ YES                 │ (stays — orchestration)
  POST /v1/analyze                │ 342-365 │ YES                 │ (stays — API endpoint)
  POST /v1/analyze_raw            │ 367-401 │ YES                 │ (stays — API endpoint)
  GET /health                     │ 404-406 │ NO                  │ health.py
  GET /version                    │ Phase 1  │ NO                  │ health.py
  _scroll_hints()                 │ 409-422 │ YES                 │ (stays — evidence producer)
  _metadata()                     │ 425-434 │ YES                 │ (stays — evidence producer)
  _server_timing()                │ 437-439 │ YES                 │ (stays — HTTP header)
```

### G4.2 Purchased splits

```text
ServerRefactor — ONLY these splits are authorized:

SPLIT 1: config.py
  Extract: _load_spatial(), all _CONSTANTS, env var resolution, configHash.
  Keep in server.py: import config; use config.MODEL_PATH, config.CONFIDENCE, etc.
  Pressure: config loading is testable independently. Currently tested only
            through full server startup.

SPLIT 2: preprocessing.py
  Extract: _preprocess().
  Keep in server.py: call preprocessing.preprocess(image) in _run_pipeline.
  Pressure: crop/resize logic is deterministic. Independently testable.
            Already has dedicated tests in test_server.py PreprocessTests.

SPLIT 3: remap.py
  Extract: _remap_coords().
  Keep in server.py: call remap.remap_coords(evidence, scale, top_px, ...).
  Pressure: coordinate remapping is a pure function. Independently testable.
            Already has dedicated tests in RemapCoordsTests.

SPLIT 4: health.py
  Extract: GET /health, GET /version, _model_id().
  Keep in server.py: import health; app.include_router(health.router).
  Pressure: health/version endpoints are independent of inference pipeline.
            Already tested in HealthTests.

SPLIT 5: yolo/inference.py
  Extract: warmup_yolo(), run_yolo_on_image(), _get_yolo_model(), YOLO_LABEL_ALIASES.
  Keep in server.py: call yolo.inference.warmup_yolo() in lifespan.
  Pressure: YOLO is independently testable with mock images. Currently tested
            only through mocked server.

SPLIT 6: ocr/rapid.py + ocr/paddle.py
  Extract: RapidOCR singleton, warmup, full-image, per-crop. PaddleOCR legacy.
  Keep in server.py: call ocr.rapid.warmup_rapid_ocr() in lifespan.
  Pressure: OCR is independently testable. Currently tested through mocked server.

STAYS in server.py:
  • FastAPI app creation + lifespan
  • _run_pipeline() orchestration
  • POST /v1/analyze, POST /v1/analyze_raw
  • _scroll_hints(), _metadata(), _server_timing()
  • Exception handler
  • OMP_NUM_THREADS import-time set

NOT authorized splits (no current pressure):
  • _run_pipeline() remains as orchestration — splitting it into a separate
    "pipeline.py" adds indirection without testability gain (pipeline IS the
    integration of yolo+ocr+fusion — testing it requires all three).
  • _scroll_hints stays — 14 lines, pure function of candidates list.
  • _metadata stays — 10 lines, pure function of static config.
```

---

## P3-G5 — PIPELINE BOUNDARY

```text
PipelineBehavior: FROZEN

The 7-stage execution pipeline is preserved exactly:

  1. DECODE              PIL.Image.open(BytesIO) or PIL.Image.frombytes(RGBA)
  2. PREPROCESS          Crop(top, bottom) → Resize(max_width) → (proc_img, scale, top_px, orig_h)
  3. YOLO                model.predict(proc_img, imgsz=640, conf=0.35, device="cpu")
  4. OCR                 RapidOCR full-image: ocr(np.asarray(rgb)[:,:,::-1], text_score=0.5)
  5. FUSION              fuse_evidence(detections, ocr_tokens, ...)
  6. COORDINATE REMAP    _remap_coords(evidence, scale, top_px, orig_w, orig_h)
  7. SERIALIZE           Add scrollHints + metadata + Server-Timing → JSON

Migration MUST NOT silently change:
  • Execution order                     — stages 1-7 in this exact sequence
  • Coordinate semantics                — full-screenshot [0,1]×[0,1], top-left origin
  • Class semantics                     — 21 raw YOLO labels → 14 canonical → label-mapping.json types
  • OCR semantics                       — RapidOCR default, full-image mode, text_score=0.5
  • Fusion behavior                     — spatial matching, chevron heuristic, search-box labeling
  • Evidence schema                     — uniclaw.localVisionEvidence.v1, all required fields

Any intentional behavior change requires a separate capability pressure
document with its own Scenario, not piggybacking on migration.
```

---

## P3-G6 — YOLO MODULE BOUNDARY

```text
YoloBoundary:

Decision: EXTRACT from backends.py into uniclaw_perception/yolo/

Module: uniclaw_perception/yolo/inference.py
  Owns:
    • YOLO model loading (_get_yolo_model with singleton cache)
    • Inference invocation (run_yolo_on_image, PIL Image input)
    • Detection postprocessing (raw ultralytics output → list[Detection])
    • Label normalization (normalize_yolo_label)
    • Model warmup (warmup_yolo with synthetic image)

Module: uniclaw_perception/yolo/labels.py
  Owns:
    • YOLO_LABEL_ALIASES dict (21 raw → 14 canonical labels)
  Extracted from backends.py lines 14-38. Currently hard-coded.
  Phase 3 moves it to a named module. Phase 4 may make it configurable.

Must NOT own:
  • Semantic Agent decisions
  • Business object identity
  • Action selection
  • GoalEvidence
  • Runtime belief
  • Capability registry

Preserved (unchanged):
  • Model artifact: best.pt (Deki-Yolo YOLOv8)
  • imgsz: 640
  • confidence: 0.35 (from label-mapping.json)
  • device: "cpu"
  • NMS: ultralytics default (built into model.predict)
  • Class vocabulary: 21 Deki-Yolo labels + 14 canonical labels

Change from current: NONE. Extract to module, imports updated, behavior identical.
```

---

## P3-G7 — OCR MODULE BOUNDARY

```text
OcrBoundary:

Decision: EXTRACT from backends.py into uniclaw_perception/ocr/

Module: uniclaw_perception/ocr/rapid.py  (active production backend)
  Owns:
    • RapidOCR singleton (_get_rapid_ocr with double-checked locking)
    • Model warmup (warmup_rapid_ocr — det kernel + rec kernel)
    • Full-image inference (run_rapid_ocr_on_image)
    • Per-crop inference (run_rapid_ocr_on_crops, _rapid_ocr_one_crop)
    • Result normalization (_normalize_rapid_result with text_score filter)

Module: uniclaw_perception/ocr/paddle.py  (legacy, comparison only)
  Owns:
    • PaddleOCR instance creation + inference
    • Moved as-is. NOT refactored. Marked as LEGACY in docstring.
  Preserved for:
    • Regression comparison (RapidOCR vs PaddleOCR output)
    • Fallback if RapidOCR ONNX runtime unavailable (unlikely)
  May be DEPRECATED in Phase 4 if RapidOCR proven sufficient.

Module: uniclaw_perception/ocr/common.py  (shared between backends)
  Owns:
    • Thread pool (_get_ocr_executor, _ocr_parallelism)
    • ROI padding (_roi_padding_px, configure_roi_padding)
    • Image crop (_crop_padded)
    • Token coordinate offset (_offset_token)

No generic OCR Provider framework.
No OCR backend registry.
No runtime backend switching beyond the existing UNICLAW_OCR_BACKEND env var.

Preserved (unchanged):
  • OCR runtime: RapidOCR (ONNX Runtime)
  • Preprocessing: PIL crop → np.asarray BGR
  • Confidence: text_score=0.5 threshold
  • Coordinate: token box in full-image pixel space
```

---

## P3-G8 — FUSION MODULE

```text
FusionBoundary:

Decision: MOVE_AS_IS with internal module split.

Current fusion.py (399 lines) contains three internal concerns.
Phase 3 splits into submodules within uniclaw_perception/fusion/:

Module: uniclaw_perception/fusion/engine.py
  Owns:
    • fuse_evidence() — primary fusion entry point
    • fuse_evidence_from_crops() — legacy crop-aligned path
    • DEFAULT_INTERACTIVE_LABELS set
  Classification: DETERMINISTIC_MECHANISM (spatial matching is deterministic)

Module: uniclaw_perception/fusion/heuristics.py
  Owns:
    • _apply_chevron_heuristic() — row-alignment menu_item reclassification
    • Search-box pre-labeling ("search" in text → type="input")
    • _primary_line_text() — multi-line token clustering
    • _merge_adjacent_boxes() — moved from server.py
  Classification: HEURISTIC_POLICY (chevron, search-box are UI heuristics;
                    they encode domain assumptions about Android Settings UI)

Module: uniclaw_perception/fusion/scoring.py
  Owns:
    • _match_score() — spatial YOLO↔OCR matching
    • _combined_confidence() — weighted YOLO+OCR confidence
    • _candidate_risks() — risk flag assignment
    • _normalized_center() — coordinate helper
  Classification: DETERMINISTIC_MECHANISM (scoring functions are deterministic)

Phase 3 reorganizes structurally. Heuristic MEANING is preserved exactly.
Phase 3 MUST NOT:
  • Change chevron row tolerance (40px) without evidence
  • Change search-box detection logic without evidence
  • Change _primary_line_text clustering without evidence
  • Add new heuristics ("maybe also check for X")

Fusion is NOT semantic authority:
  • type labels are perception evidence, not Runtime semantic truth
  • "menu_item" is a perception classification, not a navigation decision
  • Runtime independently adjudicates element navigability
```

---

## P3-G9 — CONFIG ORGANIZATION

```text
ConfigLayout:

Decision: Single config file preserved. Layout prepared for Phase 4 expansion.

Current:
  label-mapping.json  —  contains: label mappings + spatial config + detection conf

Phase 3 layout:
  config/
    label-mapping.json   —  MOVED_AS_IS from tools/local_vision/
                            Same content, same schema (uniclaw.labelMapping.v1).
                            Same configHash computation (SHA-256 of file).
                            Single source of truth — Python and C# both reference.

Phase 4 expansion landing points (NOT activated in Phase 3):
  config/
    preprocessing.json   —  Extract spatial.preprocessing to own file
    inference.json       —  Extract detection confidence, YOLO params to own file
    fusion.json          —  Extract fusion constants (currently hard-coded in fusion/*.py)

  These splits are planned but NOT executed. Phase 3 keeps label-mapping.json
  as the single config file to avoid configHash disruption.

Config loading ownership:
  Current:  server.py _load_spatial() reads label-mapping.json, populates
            module globals (_SPATIAL, _DETECTION_CONF, _CONFIG_HASH, etc.)
  Phase 3:  uniclaw_perception/config.py — Config class with:
            • Config.load(path) → Config instance
            • Config.label_mappings: dict
            • Config.spatial: dict
            • Config.detection_confidence: float
            • Config.config_hash: str
            • Config.roi_padding: dict
            • Config.preprocessing_params: dict (maxWidth, cropTop, cropBottom)
            • Respects UNICLAW_* env var overrides
            • Testable without server startup

  server.py: config = Config.load(config_path); use config.* attributes.

EffectiveConfigManifest: NOT activated in Phase 3.
  configHash = SHA-256(label-mapping.json) is preserved.
  Phase 4 will introduce canonical configId.
  Phase 3 must not make Phase 4 impossible — the Config class is a clean
  landing point for future manifest serialization.
```

---

## P3-G10 — MODEL ASSET LAYOUT

```text
ModelLayout:

Decision: Move to platforms/perception/models/. Host provides path.

Physical layout:
  platforms/perception/models/
    yolo/
      android_ui_detection_yolov8/
        best.pt                     # Production model (6.2 MB)
      yolo11n.pt                    # Legacy comparison model

Model path resolution:
  • Host sets UNICLAW_YOLO_MODEL env var to absolute path.
  • If env var absent (dev CLI): resolve relative to models/ in package tree.
  • Path resolution in config.py, not hard-coded in server.py.

Filename: "best.pt" is a packaging convention, not identity.
  modelId = SHA-256(file contents) is authoritative.
  "best.pt" could be renamed to "{modelId}.pt" in Phase 4 for clarity.
  Phase 3 preserves current filename.

Model is repository-managed:
  • Checked into git (6.2 MB — acceptable for a single model).
  • Git LFS not required at current size.
  • If model grows >50 MB or multiple model versions are stored, LFS may
    be warranted (Phase 4 decision).

No ModelRegistry.
No semantic model version lifecycle.
Phase 3 establishes stable physical location + path configuration only.
```

---

## P3-G11 — RESOURCE RESOLUTION

```text
ResourceResolution:

Decision: Explicit paths from Host/Deployment. Package-relative fallback for dev.

Resolution priority (applied in config.py Config.load()):

  1. ENVIRONMENT VARIABLE (explicit, Host-supplied)
     UNICLAW_YOLO_MODEL       → model path
     UNICLAW_LABEL_MAPPING    → config path

  2. PACKAGE-RELATIVE DEFAULT (dev/CLI fallback)
     Resolved relative to uniclaw_perception package root:
       models/yolo/android_ui_detection_yolov8/best.pt
       config/label-mapping.json

     Resolution uses: pathlib.Path(__file__).parent.parent / "models" / ...
     This works regardless of cwd.

  3. FAIL WITH CLEAR ERROR
     If neither env var nor package-relative path resolves to an existing file:
       raise FileNotFoundError(f"Model not found at {resolved_path}. "
                               f"Set UNICLAW_YOLO_MODEL or install package correctly.")

Eliminated assumptions:
  ✗ cwd-relative paths (tools/local_vision/label-mapping.json)
  ✗ ../../../ relative paths
  ✗ Implicit repo-root discovery
  ✗ sys.path manipulation to find modules

Host supplies explicit paths. Service consumes explicit paths.
Python does NOT discover architecture from repository layout at runtime.
```

---

## P3-G12 — HOST COMPATIBILITY

```text
HostCompatibility:

Decision: FULLY PRESERVED. No Host architecture changes.

All graduated Phase 2 Host contracts remain:

  • VisionServiceHost = sole Python service lifecycle owner        [PRESERVED]
  • Process launch via python -m uvicorn {package}.server:app      [PRESERVED]
  • Process-specific UDS: /tmp/uniclaw-vision-{sessionGuid}.sock   [PRESERVED]
  • Health readiness: GET /health → warm=true                      [PRESERVED]
  • Version negotiation: GET /version → supportedSchemas           [PRESERVED]
  • Shutdown: SIGTERM → wait → SIGKILL                             [PRESERVED]
  • Restart budget: configurable sliding window                    [PRESERVED]
  • Socket ownership: Host creates, passes, cleans up              [PRESERVED]

New ServiceEntryPoint:
  uniclaw_perception.server:app
  (was: tools.local_vision.server:app)

New LaunchCommand:
  {python} -m uvicorn uniclaw_perception.server:app --uds {socketPath}
  (was: {python} -m uvicorn tools.local_vision.server:app --uds {socketPath})

Environment variables passed to child (unchanged set, new package paths):
  UNICLAW_VISION_SOCKET={socketPath}
  UNICLAW_YOLO_MODEL={modelPath}
  UNICLAW_LABEL_MAPPING={configPath}
  UNICLAW_OCR_BACKEND=rapidocr
  UNICLAW_OMP_THREADS=4
  (all other UNICLAW_* vars inherited from parent)

Working directory for child process:
  Repository root (uni-agent/) or any directory — service no longer depends
  on cwd for resource resolution.

Host architecture pressure: NONE.
Host does NOT need to know about package internals.
Host only needs: service entry point string + env vars.
```

---

## P3-G13 — API CONTRACT PRESERVATION

```text
API Contract: FROZEN

All endpoints preserved with identical behavior:

  GET /health          → {"status": "ok", "warm": bool}
  GET /version         → {"supportedSchemas": [...], "serviceVersion": "...",
                          "modelId": "...", "configHash": "..."}
  POST /v1/analyze     → JPEG input → structured evidence JSON
  POST /v1/analyze_raw → RGBA raw input → structured evidence JSON

Output schema: uniclaw.localVisionEvidence.v1  [FROZEN]

Coordinate contract:
  Full-screenshot [0,1]×[0,1] normalized space.
  Top-left origin.
  Preprocessing (crop/resize) transparent to consumer.
  bounds always normalized. boundsPx always original device pixels.

Evidence contract:
  Structured evidence only — candidates, yolo, ocr, scrollHints, metadata.
  NO action decisions.
  NO semantic goals.
  NO capability selection.
  NO confidence thresholds as decisions (riskFlags are evidence tags).
```

---

## P3-G14 — PYTHON DEPENDENCY LAYOUT

```text
DependencyStrategy:

Decision: Split requirements. Pin production. Keep legacy for comparison.

Current: requirements.txt — 14 packages, mixed concerns.

Phase 3 split:

  requirements/runtime.txt  (PRODUCTION_RUNTIME):
    torch==2.2.2
    torchvision==0.17.2
    ultralytics==8.4.115
    rapidocr-onnxruntime==1.4.4
    onnxruntime==1.23.2
    paddleocr==2.10.0          # LEGACY — kept for comparison
    paddlepaddle==3.0.0         # LEGACY — kept for comparison
    pillow>=10.0.0
    numpy==1.26.4
    fastapi==0.141.1
    uvicorn[standard]==0.52.1

  requirements/dev.txt  (TEST + BENCHMARK):
    pytest>=8
    requests                    # benchmark_raw.py dependency

  REMOVED (NOT in runtime.txt):
    opencv-python==4.10.0.84         # LEGACY_UNUSED — not imported in any production path
    opencv-contrib-python==4.10.0.84 # LEGACY_UNUSED — not imported
    opencv-python-headless==4.10.0.84# LEGACY_UNUSED — not imported

  Evidence for removal: grep of all .py files in tools/local_vision/ —
  no import cv2, no import opencv. These were likely transitive dependencies
  of an earlier OCR setup. Current RapidOCR path uses PIL + numpy only.

Dependency isolation:
  • Production inference depends on: runtime.txt.
  • Training (future Phase 4) will have its own requirements/training.txt.
  • Production inference MUST NOT depend on training dependencies.

Host MUST NOT (reconfirmed):
  • pip install during Runtime startup
  • Create venv during Host startup
  • Download model during Host startup

Deployment owns provisioning. Host validates.
```

---

## P3-G15 — TEST MIGRATION

```text
Test Migration:

All 4 existing test files move with the package:
  tests/test_server.py           — imports become: from uniclaw_perception import server
  tests/test_fusion.py           — imports become: from uniclaw_perception.fusion import ...
  tests/test_backends_fusion.py  — imports become: from uniclaw_perception.yolo import ...
  tests/__init__.py              — unchanged

Test behavior must be IDENTICAL after migration.
  • Mocks patch new import paths (uniclaw_perception.* instead of tools.local_vision.*).
  • Test assertions unchanged.
  • Test fixtures (evidence JSON files) moved to tests/fixtures/.

NEW tests required (regression for migration itself):

  T1 — Package importability:
    python -c "from uniclaw_perception.server import app"
    Succeeds without cwd dependency.

  T2 — Config loading without cwd:
    cd /tmp && python -c "from uniclaw_perception.config import Config; ..."
    Config loads using package-relative paths (or fails cleanly if env vars not set).

  T3 — OLD vs NEW equivalence:
    Run old server.py AND new server.py on the SAME input image.
    Compare JSON outputs:
      • candidates count equal
      • candidate[i].type equal for all i
      • candidate[i].text equal for all i
      • candidate[i].bounds equal within 1e-6
      • candidate[i].center equal within 1e-6
      • scrollHints equal
      • metadata.schema equal
    Tolerance: 1e-6 for floating-point coordinates.
    (YOLO/OCR inference is deterministic on CPU with fixed seed.)
```

---

## P3-G16 — GOLDEN REALITY PRESERVATION

```text
RealityEquivalencePlan:

Required equivalence proofs before migration is accepted:

RE1 — Known screenshot produces structurally equivalent evidence:
  Input:  artifacts/assets/screenshots/settings-home-api35-full-20260803.png
  Old:    POST /v1/analyze on tools/local_vision server
  New:    POST /v1/analyze on uniclaw_perception server
  Assert: candidates count equal, all type/text/bounds/center equivalent.
  Tolerance: 1e-6 for coordinates (floating-point determinism).

RE2 — Coordinate normalization preserved:
  Run _remap_coords on test evidence with known scale=1.5, top_px=150.
  Old remap vs new remap: pixel-identical output.
  (This is a pure function — should be byte-identical after extraction.)

RE3 — Runtime Golden Replay unchanged:
  Full Runtime regression suite (819/819) must PASS with migrated perception.
  Architecture Guards (16/16) must PASS.
  C# adapter (LocalVisionPerceptionSource) is UNCHANGED — it calls
  the same HTTP endpoints, parses the same JSON schema.
  Runtime does NOT know the Python package moved.

RE4 — configHash stability:
  label-mapping.json is byte-identical after move.
  configHash is identical before and after migration.
  GET /version returns identical modelId + configHash.

Migration success cannot be established from unit tests only.
RE1-RE4 are REQUIRED before legacy path removal.
```

---

## P3-G17 — PROVENANCE LANDING POINT

```text
ProvenanceLandingPoint:

Phase 4 will require each perception result to carry:
  ServiceVersion, SchemaVersion, ModelId, ConfigId

Current Phase 2 already records in GET /version and /v1/analyze metadata:
  ServiceVersion    — GET /version serviceVersion
  SupportedSchemas  — GET /version supportedSchemas
  ModelId           — GET /version modelId
  ConfigHash        — GET /version configHash + metadata.configHash
  OcrBackend        — metadata.models.ocr

Phase 3 landing point: uniclaw_perception/server.py _metadata()

  def _metadata(width: int, height: int) -> dict:
      return {
          "schema": "uniclaw.localVisionEvidence.v1",
          "width": width,
          "height": height,
          "pipeline": {"name": "local-vision", "version": __version__},
          "models": {
              "yolo": config.model_path,
              "ocr": config.ocr_backend,
          },
          "configHash": config.config_hash,
          # Phase 4 additions (NOT activated now):
          # "configId": config.canonical_id,       # when EffectiveConfigManifest exists
          # "modelId": model.artifact_hash,         # when ModelRegistry exists
      }

  modelId is ALREADY in /version. Adding it to /v1/analyze metadata
  is a backward-compatible field addition (v1 schema allows extra fields
  in metadata). This is a Phase 3 bridge to Phase 4 provenance.

Provenance does NOT leak into Runtime:
  • metadata is in the JSON response. C# adapter deserializes it but
    does NOT propagate it to ObservedElement or Observation.
  • Harness may record metadata for future traceability.
  • Runtime sees only ObservedElement[] — no provenance fields.
```

---

## P3-G18 — TRAINING CODE BOUNDARY

```text
TrainingBoundary:

Current training assets: ABSENT (per audit §A23).

No training scripts, datasets, annotations, or checkpoint handling exist.
The production model is externally sourced (Deki-Yolo).

Phase 3 establishes the boundary for future training:

  Production inference package:
    uniclaw_perception/          ← production service
      yolo/
      ocr/
      fusion/

  Future training (Phase 4, NOT created now):
    platforms/perception/training/     ← training scripts
    platforms/perception/datasets/     ← dataset definitions
    platforms/perception/evaluation/   ← regression suite

  Production inference dependency graph MUST NOT include:
    • training scripts
    • dataset loaders
    • annotation tools
    • evaluation frameworks (beyond benchmark_raw.py in cli/)

  Training code may IMPORT FROM uniclaw_perception (to use inference,
  schema, fusion for evaluation). But uniclaw_perception MUST NOT
  import from training/.

No training implementation changes required in Phase 3.
Boundary is structural only — directories reserved but empty.
```

---

## P3-G19 — DEAD / LEGACY CODE

```text
LegacyRemovalRule:

Classification of non-production artifacts:

SAFE_TO_REMOVE_DURING_PHASE_3:
  1. opencv-python==4.10.0.84           [not imported anywhere]
  2. opencv-contrib-python==4.10.0.84   [not imported anywhere]
  3. opencv-python-headless==4.10.0.84  [not imported anywhere]
  Evidence: grep -r "import cv2\|from cv2\|opencv" → zero results in
  tools/local_vision/*.py. These were likely transitive deps of an
  earlier OCR or image processing setup.

DEFER (keep, do NOT delete, Phase 4 decision):
  1. yolo11n.pt                         [legacy model, comparison value]
  2. paddleocr / paddlepaddle deps      [legacy OCR backend, comparison value]
  3. PaddleOCR code paths in ocr/paddle.py [kept for regression comparison]

UNKNOWN (investigate during migration, do NOT delete without evidence):
  (none identified — all files traced to active or legacy-but-useful paths)

Deletion requires:
  • Executable evidence of non-usage (grep for imports/calls), OR
  • Clear unreachable status (dead code path, commented out, superseded).
  • At least one full CI pass after removal.
```

---

## P3-G20 — MIGRATION SEQUENCE

```text
MigrationSlices: 7 bounded slices. Sequential. Each verifiable independently.

SLICE P3-1 — Target package skeleton
  • Create platforms/perception/ directory tree.
  • Create uniclaw_perception/__init__.py with __version__.
  • Create empty subpackage __init__.py files (yolo/, ocr/, fusion/).
  • Create config/ with label-mapping.json (byte-identical copy).
  • Create models/ directory (empty, model moved in P3-4).
  • Create requirements/runtime.txt + dev.txt.
  • Create tests/__init__.py.
  VERIFY: python -c "import uniclaw_perception" succeeds.
  VERIFY: package structure matches target tree.

SLICE P3-2 — Move contract-preserving code (no refactor)
  • Move schema.py → uniclaw_perception/schema.py (MOVE_AS_IS).
  • Move fusion/fusion.py → uniclaw_perception/fusion/engine.py (MOVE_AS_IS).
    Split heuristics + scoring into submodules within fusion/.
  • Move analyze.py → cli/analyze.py (MOVE_AND_RENAME).
  • Move benchmark_raw.py → cli/benchmark_raw.py (MOVE_AS_IS).
  • Update imports in moved files (fusion imports from ..schema, etc.).
  VERIFY: python -c "from uniclaw_perception.fusion.engine import fuse_evidence"
  VERIFY: existing fusion tests pass with new import paths.

SLICE P3-3 — Extract purchased server.py responsibilities
  • Create config.py (extract _load_spatial, constants, env resolution).
  • Create preprocessing.py (extract _preprocess).
  • Create remap.py (extract _remap_coords).
  • Create health.py (extract /health, /version, _model_id).
  • Create yolo/inference.py (extract YOLO loading, inference, warmup).
  • Create yolo/labels.py (extract YOLO_LABEL_ALIASES).
  • Create ocr/rapid.py (extract RapidOCR singleton, warmup, inference).
  • Create ocr/paddle.py (move PaddleOCR code as-is, mark LEGACY).
  • Create ocr/common.py (extract shared: thread pool, padding, crop, offset).
  • Update server.py: import from extracted modules, keep orchestration.
  VERIFY: all existing tests pass with new import paths.
  VERIFY: config.py loads correctly with package-relative paths.

SLICE P3-4 — Move config + model assets
  • Copy label-mapping.json → config/label-mapping.json (byte-identical).
  • Move best.pt → models/yolo/android_ui_detection_yolov8/best.pt.
  • Move yolo11n.pt → models/yolo/yolo11n.pt.
  • Move evidence JSON files → tests/fixtures/.
  • Update config.py to resolve model path via env var or package-relative.
  VERIFY: configHash unchanged (file is byte-identical).
  VERIFY: modelId unchanged (file is byte-identical).

SLICE P3-5 — Host entrypoint update
  • Update VisionServiceHost: new ServiceEntryPoint = "uniclaw_perception.server:app".
  • Update LaunchCommand: uvicorn uniclaw_perception.server:app --uds {socket}.
  • Update env vars passed to child (model path, config path → new locations).
  • No Host architecture changes.
  VERIFY: Host starts service successfully. GET /health → warm=true.
  VERIFY: GET /version returns expected modelId + configHash.

SLICE P3-6 — Reality equivalence validation
  • Run RE1 (known screenshot equivalence).
  • Run RE2 (coordinate remap equivalence).
  • Run RE3 (Runtime Golden Replay, 819/819 + 16/16).
  • Run RE4 (configHash + modelId stability).
  VERIFY: all 4 reality equivalence checks PASS.

SLICE P3-7 — Legacy path removal
  • Remove tools/local_vision/ (all files).
  • Remove artifacts/local-vision/models/ (moved to platforms/perception/models/).
  • Remove artifacts/local-vision/*.evidence.json (moved to tests/fixtures/).
  • Update any remaining references to old paths (CI scripts, documentation).
  VERIFY: full CI pass with NO references to tools/local_vision/.
  VERIFY: nothing imports from tools.local_vision.
```

---

## P3-G21 — ROLLBACK DURING MIGRATION

```text
RollbackPlan:

During migration (slices P3-1 through P3-6):
  • tools/local_vision/ REMAINS functional.
  • Both old AND new paths can serve perception requests.
  • Host can be configured to launch OLD service (tools.local_vision.server:app)
    OR NEW service (uniclaw_perception.server:app) via ServiceEntryPoint config.
  • Runtime regression runs against whichever service Host launches.
  • Reality equivalence tests (RE1-RE4) compare OLD vs NEW output.

Rollback trigger:
  If any slice P3-1 through P3-6 fails verification:
    • Stop migration. Fix issue. Re-run slice.
    • OLD service remains operational throughout.
    • No downtime. No degraded perception.

After P3-7 (legacy path removal):
  • Rollback = git revert the migration commit.
  • tools/local_vision/ restored from git history.
  • Host ServiceEntryPoint reverted to old path.
  • models/ and config/ paths reverted.

No permanent dual implementation:
  • Dual paths exist ONLY during P3-1 through P3-6.
  • After P3-7 verification, old paths are REMOVED.
  • There is exactly ONE production path: uniclaw_perception.
```

---

## P3-G22 — ARCHITECTURE GUARDS

```text
Architecture Guards (post-migration):

Existing guards preserved:
  G1: Runtime → Python implementation                    FORBIDDEN
  G2: Runtime → VisionServiceHost                        FORBIDDEN
  G3: IEnvironment unchanged                             VERIFIED
  G4: Observation / ObservedElement unchanged             VERIFIED
  G5: Adapter-private interfaces unchanged                VERIFIED

New guards for Phase 3:
  G6: uniclaw_perception → Runtime semantic types         FORBIDDEN
      (Python package does NOT import, reference, or
       serialize any Runtime .NET type or concept.
       Evidence: schema.py types are Box, Detection, OcrToken —
       all perception-internal.)
  G7: uniclaw_perception → Runtime decision authority     FORBIDDEN
      (No action decisions, no semantic goals, no capability
       selection in Python output.)
  G8: training/ → uniclaw_perception/                    FUTURE ONE-WAY
      (Training may import from perception package.
       Perception MUST NOT import from training.
       Enforced in Phase 4 when training/ exists.)
  G9: config/ → uniclaw_perception/                      ALLOWED (read-only)
      (Config is the single source of truth. Service reads it.)
  G10: models/ → uniclaw_perception/                     ALLOWED (read-only)
      (Service loads model artifact. Does not write.)

No guard proliferation. Existing dependency direction is sufficient
to prove the Runtime←Adapter←Perception boundary.
```

---

## P3-G23 — PHASE 4 DEFERRED BOUNDARY

```text
Phase4Boundary: FROZEN

Explicitly kept OUT of Phase 3:

  ✗ PerceptionConfigManifest activation
  ✗ Canonical configId (SHA-256 of effective config manifest)
  ✗ ModelManifest / ModelRegistry
  ✗ Semantic model versions (1.0.0, 2.0.0)
  ✗ Model lifecycle states (CANDIDATE → VALIDATED → PROMOTED → ACTIVE)
  ✗ Model promotion rule implementation
  ✗ DatasetRegistry
  ✗ Annotation workflow
  ✗ Training run provenance
  ✗ Evaluation governance
  ✗ Regression suite (accuracy, not just latency)
  ✗ Deployment promotion
  ✗ Automatic rollback
  ✗ Failure → training dataset automation
  ✗ Golden evaluation dataset creation
  ✗ model_card.md

Phase 3 prepares landing points only:
  • config/ directory exists (future EffectiveConfigManifest lives here)
  • models/ directory exists (future ModelRegistry lives adjacent)
  • _metadata() enriched with modelId (bridge to Phase 4 provenance)
  • Config class is a clean serialization point (future configId)
  • Package __version__ is a clean service version anchor

Phase 4 can activate these without restructuring Phase 3 work.
```

---

## P3-G24 — ADMISSION CRITERIA

```text
AdmissionCriteria: 10 gates. ALL must pass before migration is accepted.

AC1  — Target package tree approved.
       File: this document §G2.2.
       Status: APPROVED in this gate.

AC2  — File migration map complete.
       File: this document §G1.1.
       Status: COMPLETE. 15 files, 6 classifications.

AC3  — Host entrypoint strategy approved.
       File: this document §G12.
       Status: APPROVED. ServiceEntryPoint + LaunchCommand defined.
       Host architecture unchanged.

AC4  — Current pipeline behavior frozen.
       File: this document §G5.
       Status: FROZEN. 7-stage pipeline preserved exactly.

AC5  — Config/model path ownership approved.
       File: this document §G9, §G10, §G11.
       Status: APPROVED. Host supplies explicit paths. Package-relative fallback.

AC6  — No Runtime boundary change.
       IEnvironment, Observation, ObservedElement unchanged.
       Status: VERIFIED. C# adapter unchanged.

AC7  — Python production/training boundary explicit.
       File: this document §G18.
       Status: APPROVED. Separate directories. One-way import dependency.

AC8  — Migration rollback defined.
       File: this document §G21.
       Status: APPROVED. Dual-path during migration. Git revert after.

AC9  — Reality equivalence tests defined.
       File: this document §G16.
       Status: APPROVED. RE1-RE4 defined with tolerances.

AC10 — No Phase 4 governance accidentally pulled in.
       File: this document §G23.
       Status: VERIFIED. 14 Phase 4 items explicitly excluded.
```

---

## AGGREGATE DECISION

```text
PERCEPTION_PLATFORM_PHASE_3_PYTHON_SERVICE_MIGRATION_AND_REFACTOR_GATE_RESULT
  = PURCHASE_WITH_CONSTRAINTS

CurrentImplementation:
  tools/local_vision/  —  well-audited, behaviorally characterized,
  13 source files + 3 model/config artifacts + 4 test files.
  All pipeline stages traced. All config items inventoried.
  All failure modes documented. Ready for structural migration.

TargetPackage:
  uniclaw_perception  (Python 3.11 package)

TargetTree:
  platforms/perception/
    uniclaw_perception/    (production package: server, config, yolo, ocr, fusion)
    config/                (label-mapping.json)
    models/                (YOLO artifacts)
    cli/                   (developer tools)
    tests/                 (Python test suite + fixtures)
    requirements/          (runtime.txt + dev.txt)

MigrationMap:
  10 MOVE_AS_IS, 2 SPLIT, 1 REFACTOR, 1 MOVE_AND_RENAME,
  2 KEEP_TEMPORARILY, 2 KEEP.
  0 behavioral changes. 0 semantic changes.

ServerRefactor:
  6 purchased splits: config.py, preprocessing.py, remap.py, health.py,
  yolo/inference.py + yolo/labels.py, ocr/rapid.py + ocr/paddle.py + ocr/common.py.
  0 framework layers. 0 "pipeline.py" abstraction.
  _run_pipeline() stays in server.py as orchestration.

PipelineBehavior:
  FROZEN — 7 stages, execution order, coordinate semantics, class semantics,
  OCR semantics, fusion behavior, evidence schema all preserved.

YoloBoundary:
  Extract to uniclaw_perception/yolo/. Model loading + inference + label
  normalization. No behavioral changes.

OcrBoundary:
  Extract to uniclaw_perception/ocr/. RapidOCR active + PaddleOCR legacy.
  Shared utilities in ocr/common.py. No generic Provider framework.

FusionBoundary:
  Move to uniclaw_perception/fusion/. Internal split: engine, heuristics, scoring.
  Heuristic meaning preserved. Fusion is NOT semantic authority.

ConfigLayout:
  Single file: config/label-mapping.json (byte-identical move).
  Config class in config.py. Phase 4 expansion landing points reserved.
  configHash preserved (SHA-256 of file, unchanged).

ModelLayout:
  models/yolo/android_ui_detection_yolov8/best.pt.
  Host provides path via env var. Package-relative fallback.
  modelId = SHA-256(content) — preserved.

ResourceResolution:
  Priority: env var → package-relative → fail.
  cwd independence achieved.
  No implicit repo-root discovery.

HostCompatibility:
  FULLY PRESERVED. ServiceEntryPoint: uniclaw_perception.server:app.
  LaunchCommand: uvicorn uniclaw_perception.server:app --uds {socket}.
  No Host architecture changes.

NewServiceEntryPoint:
  uniclaw_perception.server:app

NewLaunchCommand:
  {python} -m uvicorn uniclaw_perception.server:app --uds {socketPath}

DependencyStrategy:
  Split: runtime.txt (production) + dev.txt (test/benchmark).
  3 opencv packages REMOVED (unused, grep-verified).
  paddleocr + paddlepaddle KEPT for comparison.

TrainingBoundary:
  Structural only. training/ directory reserved, empty.
  One-way import: training → perception, NOT perception → training.

ProvenanceLandingPoint:
  _metadata() enriched with modelId (backward-compatible field addition).
  Config class ready for future configId serialization.
  Provenance does NOT leak into Runtime.

RealityEquivalencePlan:
  RE1: Known screenshot equivalence (candidates, bounds, text).
  RE2: Coordinate remap pixel-identical.
  RE3: Runtime Golden Replay (819/819 + 16/16).
  RE4: configHash + modelId stability.

MigrationSlices:
  P3-1: Package skeleton → P3-2: Move code → P3-3: Extract refactors →
  P3-4: Move assets → P3-5: Host update → P3-6: Reality equivalence →
  P3-7: Legacy removal.
  Sequential. Each independently verifiable.

RollbackPlan:
  Dual-path during slices P3-1 through P3-6.
  Git revert after P3-7.
  No permanent dual implementation.

LegacyRemovalRule:
  3 opencv packages: SAFE_TO_REMOVE (grep-verified zero imports).
  yolo11n.pt + paddleocr: DEFER (comparison value).
  Deletion requires CI pass.

RuntimeDelta:
  NONE

SemanticDelta:
  NONE

AuthorityDelta:
  NONE

Phase4Boundary:
  FROZEN — 14 items explicitly excluded.

AuthorizedImplementationScope:
  Phase 3 Python service migration as specified in this gate.
  Must execute slices P3-1 through P3-7 in sequence.
  Must pass all 10 admission criteria.
  Must pass all 4 reality equivalence checks (RE1-RE4).
  Must preserve Phase 2 Host contract.
  Must preserve Phase 1 API contract.
  Full Runtime regression: 819/819 + Architecture Guards: 16/16.

ForbiddenScope:
  • No behavioral changes to perception pipeline.
  • No YOLO/OCR/fusion algorithm changes.
  • No configHash or modelId changes.
  • No new API endpoints.
  • No schema changes.
  • No Runtime type or interface changes.
  • No Host architecture changes.
  • No Phase 4 governance activation.
  • No training pipeline implementation.
  • No model registry.
  • No configId / EffectiveConfigManifest.
  • No dataset or annotation tooling.

NextTask:
  PROJECT_LEADER_PERCEPTION_PLATFORM_PHASE_3_IMPLEMENTATION
  (requires separate task authorization — implementation authority
   is NOT granted by this gate)

NO_AUTOMATIC_IMPLEMENTATION
```

STOP.
