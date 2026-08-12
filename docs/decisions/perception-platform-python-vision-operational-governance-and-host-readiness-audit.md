# Perception Platform — Python Vision Operational Governance & Host Readiness Audit

> Date: 2026-08-12
> Role: Project Leader / Architecture & Engineering Auditor
> Mode: `READ_ONLY_ARCHITECTURE_AND_ENGINEERING_AUDIT`
> Inputs:
> - Phase 1: `PERCEPTION_PLATFORM_PHASE_1_CONTRACT_EXTRACTION = VALIDATED`
> - Phase 2 Review: `PURCHASE_WITH_CONSTRAINTS`
> - Repository: `uni-claw/tools/local_vision/` (authoritative)
> Result: `PERCEPTION_PLATFORM_PYTHON_VISION_OPERATIONAL_GOVERNANCE_AND_HOST_READINESS_AUDIT_RESULT`
> Audit Status: **READY_WITH_MINIMAL_FIXES**
> Implementation authority: **NOT GRANTED**

---

## A1 — CURRENT PYTHON SERVICE INVENTORY

### A1.1 Artifact classification

| # | File | Classification | Future Action |
|---|---|---|---|
| 1 | `server.py` | PRODUCTION_RUNTIME | REFACTOR_DURING_MIGRATION (Host compatibility: socket injection, SIGTERM) |
| 2 | `backends.py` | PRODUCTION_RUNTIME | MOVE_AS_IS |
| 3 | `fusion.py` | PRODUCTION_RUNTIME | MOVE_AS_IS |
| 4 | `schema.py` | PRODUCTION_RUNTIME | MOVE_AS_IS |
| 5 | `analyze.py` | PRODUCTION_RUNTIME (CLI) | MOVE_AS_IS |
| 6 | `label-mapping.json` | CONFIGURATION | MOVE_AS_IS (single source of truth) |
| 7 | `requirements.txt` | DEPLOYMENT | MOVE_AS_IS (pin versions) |
| 8 | `benchmark_raw.py` | EVALUATION | MOVE_AS_IS (performance only; not accuracy regression) |
| 9 | `tests/test_server.py` | TEST | MOVE_AS_IS |
| 10 | `tests/test_fusion.py` | TEST | MOVE_AS_IS |
| 11 | `tests/test_backends_fusion.py` | TEST | MOVE_AS_IS |
| 12 | `tests/__init__.py` | TEST | MOVE_AS_IS |
| 13 | `__init__.py` | PRODUCTION_RUNTIME | MOVE_AS_IS |

### A1.2 Model artifacts (external to tools/local_vision/)

| # | File | Classification | Size | Notes |
|---|---|---|---|---|
| M1 | `artifacts/local-vision/models/android_ui_detection_yolov8/best.pt` | MODEL_ARTIFACT (PRODUCTION) | 6.2 MB | Deki-Yolo YOLOv8, 21 labels |
| M2 | `artifacts/local-vision/models/yolo11n.pt` | MODEL_ARTIFACT (LEGACY/ALT) | 5.6 MB | Not used in production; yolo11n was previous default |

### A1.3 Dataset / training artifacts

```text
TrainingAssets:      ABSENT (no training scripts, dataset definitions, or annotation tooling found)
DatasetAssets:       ABSENT (no labeled datasets in repository)
EvaluationAssets:    PARTIAL (benchmark_raw.py is latency benchmark only, not accuracy regression)
```

### A1.4 Reality assets (consumed by perception)

| Asset | Location | Type |
|---|---|---|
| Screenshot: settings-home-api35-full-20260803.png | `artifacts/assets/screenshots/` | E4 recorded emulator |
| Screenshot: settings-diag-20260803.png | `artifacts/assets/screenshots/` | E4 recorded emulator |
| Evidence: vision_test_controlled_screen.evidence.json | `artifacts/local-vision/` | E3 executable replay |
| Evidence: settings-real.android-ui-yolo.evidence.json | `artifacts/local-vision/` | E3 executable replay |

---

## A2 — ACTUAL EXECUTION PIPELINE

### A2.1 Exact pipeline (from server.py `_run_pipeline`)

```text
1. DECODE
   JPEG:  PIL.Image.open(BytesIO(image_bytes))           [zero-copy header parse]
   RAW:   PIL.Image.frombytes("RGBA", w, h, body)
          .convert("RGB")                                [memory wrap, 0ms decode]

2. PREPROCESS  (_preprocess)
   a. Crop top:    orig_h * _CROP_TOP (default 0.0625)   [removes status bar]
   b. Crop bottom: orig_h * _CROP_BOTTOM (default 0.0625) [removes nav bar]
   c. Resize:      if width > _MAX_WIDTH (720):
                     scale = orig_w / 720
                     new_h = cropped_h / scale
                     PIL.Image.LANCZOS resize
   Returns: (proc_img, scale, top_px, orig_h)

3. YOLO  (run_yolo_on_image)
   Model:  _get_yolo_model(model_path)  [module-level singleton cache,
            keyed by model_path string.  First call loads best.pt (3-5s).]
   Infer:  model.predict(source=PIL.Image, imgsz=640, conf=0.35, device="cpu")
   Output: list[Detection] with raw YOLO labels → normalize_yolo_label()
           (YOLO_LABEL_ALIASES: 21 raw labels → 14 canonical labels)

4. OCR  (default: rapidocr, full-image mode)
   Model:  _get_rapid_ocr()  [process-level singleton, double-checked locking.
            RapidOCR() constructs ONNX sessions for det+rec (1-3s).
            warmup_rapid_ocr() runs synthetic images through det+rec kernels.]
   Infer:  rgb = image.convert("RGB")
           output = ocr(np.asarray(rgb)[:, :, ::-1])     [BGR for ONNX]
           _normalize_rapid_result(raw, text_score=0.5)
   Output: list[OcrToken], full-image coordinates

   Alternative (roi mode, UNICLAW_OCR_MODE=roi):
     Filter YOLO dets to text-bearing labels → merge adjacent → crop each →
     RapidOCR per crop → offset tokens back to full-image coords.
     Slower (N × DBNet) but higher text precision on sparse pages.

5. FUSION  (fuse_evidence)
   a. Filter YOLO dets to interactive labels (DEFAULT_INTERACTIVE_LABELS)
   b. Spatial match: each YOLO box → OCR tokens within distance
      (max_distance = screen_diag × 0.055)
   c. Primary line text extraction (per YOLO box, top row clustering)
   d. Candidate assembly: type=detection.label, text=primary_text,
      confidence=combined(yolo×0.72 + ocr×0.28), bounds=normalized, riskFlags
   e. Promote unmatched OCR → text_block candidates (promote_unmatched_ocr=True)
   f. Search-box pre-labeling: text containing "search" → type="input"
   g. Chevron heuristic: OCR text on same row as icon/switch/toggle/checkbox
      → reclassify to menu_item

6. COORDINATE REMAP  (_remap_coords)
   If preprocessing was applied (scale≠1.0 or top_px≠0):
     All boundsPx/centerPx → remap to original full-screenshot pixel space.
     All bounds/center/coordinate → recompute normalized from remapped pixels.
   Idempotent if no preprocessing.

7. SERIALIZE
   Add scrollHints (_scroll_hints), metadata (_metadata), Server-Timing header.
   Return JSON response.
```

### A2.2 Cached resources (process-global, survives across requests)

| Resource | Type | Init cost | Location |
|---|---|---|---|
| YOLO model | ultralytics.YOLO singleton | 3-5s (load .pt + compile) | `backends._yolo_model_cache` dict |
| RapidOCR instance | rapidocr_onnxruntime.RapidOCR singleton | 1-3s (ONNX session init) | `backends._rapid_ocr_singleton` |
| OCR thread pool | ThreadPoolExecutor(4) | ~0ms | `backends._ocr_executor` |
| Label mapping config | dict (parsed JSON) | ~0ms | `server._SPATIAL` |
| Config hash | str (SHA-256) | ~0ms | `server._CONFIG_HASH` |
| Warm flag | bool | — | `server._WARM` |
| ROI padding spec | dict | ~0ms | `backends._ROI_PADDING_SPEC` |

### A2.3 Per-request state

| Resource | Lifetime | Notes |
|---|---|---|
| JPEG/raw body decode | Request scope | PIL Image, garbage collected post-response |
| Preprocessed image | Request scope | Resized/cropped PIL Image |
| YOLO detections list | Request scope | list[Detection] |
| OCR tokens list | Request scope | list[OcrToken] |
| Evidence dict | Request scope | Serialized to JSON, returned |

### A2.4 Warmup behavior

```text
lifespan startup (before any request accepted):
  1. _load_spatial()          — parse label-mapping.json, compute configHash
  2. warmup_yolo()            — load model + run inference on 640×640 black image
  3. warmup_rapid_ocr()       — construct RapidOCR + run det kernel on black
                                 image + rec kernel on synthetic text line
  4. _WARM = True

warmup_yolo failure → unhandled exception → server fails to start.
warmup_rapid_ocr failure → caught, logged, server starts anyway
  (first real request pays the kernel init cost).
```

### A2.5 Failure handling

| Failure point | Behavior |
|---|---|
| Invalid image bytes | PIL.Image.open raises → FastAPI returns 500 |
| YOLO model not loaded | RuntimeError raised at first predict call |
| OCR not loaded | RuntimeError raised at first OCR call |
| Fusion exception | Unhandled → FastAPI returns 500 |
| Any unhandled exception | `unhandled_exception_handler` → 500 + `{detail: "Type: message"}` |

---

## A3 — PROVIDER HOST READINESS

```text
ServiceEntryPoint:
  tools/local_vision/server.py:app   (FastAPI app object)

LaunchCommand:
  python -m uvicorn tools.local_vision.server:app --uds {socketPath}
  (current manual launch, no Host automation)

PythonExecutable assumptions:
  Default: "python3" resolved from PATH.
  Override: UNICLAW_PYTHON_BIN env var (not yet implemented — Phase 2 Host will add).
  Validated version: ≥ 3.11 (current .venv is Python 3.11).

WorkingDirectory assumptions:
  Repository root (uni-claw/ or uni-agent/) so relative paths resolve:
  - tools/local_vision/label-mapping.json
  - artifacts/local-vision/models/android_ui_detection_yolov8/best.pt

Required environment variables:
  UNICLAW_YOLO_MODEL        — model path (default: artifacts/.../best.pt)
  UNICLAW_LABEL_MAPPING     — config path (default: tools/local_vision/label-mapping.json)
  UNICLAW_OCR_BACKEND       — "rapidocr" (default) or "paddleocr"
  UNICLAW_OCR_MODE          — "full" (default) or "roi"
  UNICLAW_OMP_THREADS       — OpenMP threads (default: 4)
  UNICLAW_OCR_LANG          — PaddleOCR language (default: "en"; rapidocr ignores)
  UNICLAW_OCR_TEXT_SCORE    — RapidOCR text confidence threshold (default: 0.5)
  UNICLAW_OCR_PARALLEL      — OCR worker threads (default: 4)
  UNICLAW_IMAGE_MAX_WIDTH   — preprocess max width (default: 720)
  UNICLAW_IMAGE_CROP_TOP    — preprocess top crop ratio (default: 0.0625)
  UNICLAW_IMAGE_CROP_BOTTOM — preprocess bottom crop ratio (default: 0.0625)
  UNICLAW_VISION_SOCKET     — UDS path (NOT YET IMPLEMENTED — Phase 2 Host will add)

Model path resolution:
  server.py line 42: _MODEL_PATH = os.environ.get("UNICLAW_YOLO_MODEL",
    "artifacts/local-vision/models/android_ui_detection_yolov8/best.pt")
  Resolved relative to working directory (repository root).

Config path resolution:
  server.py lines 75-76: path = Path(os.environ.get("UNICLAW_LABEL_MAPPING",
    "tools/local_vision/label-mapping.json"))
  Resolved relative to working directory (repository root).

UDS path configuration:
  Current: server.py does NOT accept socket path configuration.
  uvicorn --uds <path> binds the server. Path chosen by launcher.
  Current fixed practice: /tmp/uniclaw-vision.sock
  Phase 2 Host will inject via UNICLAW_VISION_SOCKET → server.py reads it.

uvicorn worker/process topology:
  Single uvicorn worker (default). No --workers flag.
  Single OS process. All module-level singletons are process-local.
  Concurrency: async (FastAPI), but YOLO/OCR inference is synchronous
  and blocks the event loop during inference (~2-4s per request).

Signal handling:
  uvicorn handles SIGTERM/SIGINT → graceful shutdown.
  server.py lifespan shutdown handler → (currently empty, yields after warmup).
  No custom signal handlers in server.py.

Shutdown behavior:
  uvicorn stops accepting new requests.
  In-flight requests complete or timeout.
  Process exits.
  Socket file REMAINS on disk (uvicorn does not clean up --uds socket).

Exit code behavior:
  Normal shutdown: 0.
  Unhandled exception during startup (model load failure): non-zero.
  Unhandled exception during request: 500 response, process continues.

Stale socket behavior:
  Current: if /tmp/uniclaw-vision.sock exists from previous crashed instance,
  uvicorn --uds fails with "address already in use."
  Manual cleanup required before restart.
  Phase 2 Host: process-specific UDS with staleness detection (REVIEW 4).

Health semantics:
  GET /health → {"status": "ok", "warm": bool}
  warm=true: lifespan startup completed (YOLO loaded + OCR initialized).
  warm=false: startup in progress.
  Does NOT prove: pipeline produces valid output for a real image.

Version semantics:
  GET /version (Phase 1 addition):
  {
    "supportedSchemas": ["uniclaw.localVisionEvidence.v1"],
    "serviceVersion": "1.0",
    "modelId": "android_ui_detection_yolov8/{sha256_12}",
    "configHash": "{sha256_64}"
  }
```

---

## A4 — PROCESS-SPECIFIC UDS

```text
SocketConfiguration:
  Current:  /tmp/uniclaw-vision.sock  (fixed, single instance per machine)
  Target:   /tmp/uniclaw-vision-{sessionGuid}.sock  (process-specific)

Injection mechanism:
  Environment variable: UNICLAW_VISION_SOCKET
  Rationale: smallest mechanism. uvicorn --uds already reads the path.
  server.py currently does NOT read UNICLAW_VISION_SOCKET — this is a
  Phase 2 minimal fix.

Required server.py change (Phase 2 minimal fix #1):
  In lifespan startup or app-level config, read:
    socket_path = os.environ.get("UNICLAW_VISION_SOCKET")
  If set, the service is running under Host management.
  The service does NOT need to act on this — uvicorn --uds handles binding.
  The env var is for Host→Service identification only.
  (Actually, uvicorn --uds already receives the path. The env var is for
   the service to KNOW its socket path for diagnostic/logging purposes.)

Safe cleanup rules:
  1. Only delete paths matching /tmp/uniclaw-vision-{uuid}.sock pattern.
  2. Before deletion: attempt connect + GET /health (500ms timeout).
  3. If /health returns 200 → socket is LIVE → DO NOT DELETE (collision).
  4. If connect refused or timeout → stale → safe to remove.
  5. Never delete paths not matching the Host's naming pattern.
  6. Never delete paths owned by a different user (if detectable via os.stat).
  7. On graceful shutdown: Host removes its own socket after child exits.
  8. On crash: stale socket cleaned at next Host startup (step 7 of startup sequence).
```

---

## A5 — READINESS SEMANTICS

```text
ReadinessSemantics:

Current GET /health warm=true proves:
  PROCESS_RUNNING:    YES (FastAPI is responding to HTTP)
  SERVICE_BOUND:      YES (uvicorn bound to UDS)
  MODEL_LOADED:       YES (warmup_yolo completed — model loaded + one inference)
  OCR_READY:          YES (warmup_rapid_ocr completed — ONNX sessions + kernel init)
  PIPELINE_READY:     PARTIAL (model + OCR are loaded, but no end-to-end pipeline
                       test with a real image has run)
  REQUEST_READY:      NO (health does not prove /v1/analyze returns valid evidence)

Distinction:
  warm=true means "infrastructure is loaded." It does NOT mean "pipeline
  produces correct output." A model file could be corrupted in a way that
  survives loading but fails on inference. A config change could make fusion
  produce empty candidates.

  This is acceptable for Phase 2. Adding a pipeline smoke test (run a known
  image through the full pipeline during warmup) is Phase 3/4 governance work.

GET /version after readiness:
  SAFE. /version reads static data (modelId from SHA-256 of model file,
  configHash from SHA-256 of config file). No inference required.
  Meaningful: provides deployment identity after readiness is proven.
```

---

## A6 — MODEL IDENTITY

```text
ModelIdentity:

Current (Phase 1):
  modelId = SHA-256(model file contents, first 12 hex chars)
  Format:   android_ui_detection_yolov8/{sha256_12}
  Source:   server.py _model_id() (Phase 1 addition)

Canonical model identity tuple (proposed):
  ModelName:           "android_ui_detection_yolov8"
  ModelArtifactHash:   SHA-256(full file) — AUTHORITATIVE for binary identity
  ModelFormat:         "ultralytics-yolov8" (PyTorch .pt)
  ModelFamily:         "Deki-Yolo" (21-class UI element detection)
  OptionalSemanticVersion:  null (not yet assigned — model has no version beyond hash)

ArtifactHash is authoritative.
"best.pt" is NOT identity — it's a filename convention, not a version.
mtime / path / filename MUST NOT be used as canonical identity.

Current model inventory:
  android_ui_detection_yolov8/best.pt   — 6,221,290 bytes, SHA-256 TBD at audit time
  yolo11n.pt                            — 5,613,764 bytes, LEGACY (not used in production)

  Only one model is ACTIVE. No version history. No predecessor tracking.
```

---

## A7 — MODEL VERSION MAINTENANCE

```text
ModelVersionCurrentState:
  Single model artifact. No version management. "best.pt" is convention.
  No semantic version. No predecessor tracking. No rollback target.
  Model identity is purely artifact-hash-based (Phase 1 modelId).

ModelLifecycleTarget (minimum sustainable, future):

States:
  CANDIDATE    — new model artifact produced by training
  VALIDATED    — evaluation metrics meet thresholds
  PROMOTED     — regression comparison against ACTIVE passed + review approved
  ACTIVE       — deployed to production vision service
  RETIRED      — previously ACTIVE, superseded
  REJECTED     — failed evaluation/regression/review

ROLLED_BACK is an EVENT, not a state:
  ACTIVE model V2 → regression failure in production
  → PROMOTED model V1 → re-activated as ACTIVE
  V2 transitions to RETIRED (with rollback reason recorded).
  V1 transitions from RETIRED to ACTIVE.

  No new state needed. Rollback is a transition pair, not a persistent label.

Model version record (future manifest, minimum fields):
  model_name:            "android_ui_detection_yolov8"
  semantic_version:      "1.0.0"  (human label, optional)
  artifact_hash:         "sha256:abc123..."
  training_dataset:      "settings-screenshots-chinese-rom/2026-08-01"
  training_code_revision: "abc123def"  (git commit)
  config_version:        "sha256:def456..."  (config used for training)
  class_vocabulary:      ["text_block", "switch", "icon", ...]  (21 labels)
  evaluation_dataset:    "settings-golden/2026-08-01"
  evaluation_metrics:    { mAP50: 0.76, ... }
  promotion_decision:    "PROMOTED"
  promotion_timestamp:   "2026-08-12T..."
  predecessor_model:     "sha256:789abc..."  (previous ACTIVE)
  rollback_target:       null  (or predecessor hash if this was a rollback)
  provenance:            "trained from Deki-Yolo base, fine-tuned on 200 screenshots"
  status:                "ACTIVE"
```

---

## A8 — MODEL PROMOTION RULE

```text
ModelPromotionRule:

A new model artifact SHALL NOT become ACTIVE merely because:
  • training completed, OR
  • best.pt file exists, OR
  • a human placed it in the artifacts/ directory.

Promotion REQUIRES (all gates must pass):

1. ARTIFACT IDENTITY
   modelId computed (SHA-256 of artifact).
   Artifact is immutable. No in-place replacement of ACTIVE model.

2. KNOWN DATASET PROVENANCE
   Training dataset is versioned and identified by DATASET_ID.
   Dataset includes annotation provenance records.

3. EVALUATION RESULTS
   Evaluation metrics computed against a fixed golden evaluation dataset.
   Metrics include: mAP@0.5, per-class precision/recall, bounding-box quality.

4. REGRESSION COMPARISON AGAINST ACTIVE
   Candidate evaluated on the SAME golden dataset as current ACTIVE.
   No regression beyond defined thresholds (see §A9).

5. COMPATIBILITY WITH SERVICE SCHEMA
   Candidate model's class vocabulary is compatible with current label mapping.
   Candidate model's output format is compatible with current pipeline.

6. EXPLICIT PROMOTION DECISION
   Human or automated governance system records promotion.
   Timestamp, decision rationale, approver identity.

Current state: NO promotion governance exists. Model is ACTIVE by convention
(best.pt in artifacts/). Phase 4 governance will implement this rule.
```

---

## A9 — MODEL REGRESSION CLOSURE

```text
RegressionClosure:

Current evaluation assets:
  benchmark_raw.py: Latency benchmark only (p50, p95, p99). NOT accuracy regression.
  tests/test_server.py: Unit tests with mocked YOLO/OCR. NOT model evaluation.
  Existing reality screenshots: 2 screenshots in artifacts/assets/screenshots/.
  Existing evidence files: 3 JSON files in artifacts/local-vision/.

  NO accuracy regression suite exists. NO golden evaluation dataset.
  NO comparison framework between candidate and ACTIVE model.

Target regression suite (future, Phase 4):
  Harness Reality Corpus
    └─ golden-screenshots/  (E4 recorded screenshots with ground-truth annotations)
         ↓
  Perception Evaluation Corpus
    └─ Pinned golden set: N screenshots with human-annotated ground truth
         ↓
  Candidate Model vs ACTIVE Model
    └─ Both models run on identical golden set
         ↓
  Evaluation Metrics (minimum):
    mAP@0.5:          detection mean Average Precision
    Per-class precision/recall: identify class-specific degradation
    Bounding-box IoU:  detect coordinate regression
    Class confusion:   detect label swapping (e.g., "switch" → "checkbox")
    OCR impact:        candidate fusion accuracy (type+text+bounds correctness)
    UNKNOWN preservation: false-positive rate (hallucinated detections)

  Regression thresholds (from architecture gate §5.5):
    mAP@0.5:            no regression > 2% absolute
    OCR char accuracy:  no regression > 1% absolute
    Fusion accuracy:    no regression > 2% absolute
    Switch state:       no regression > 3% absolute
    Latency p50:        no regression > 20% relative (WARN)
    Latency p99:        no regression > 50% relative (BLOCK)
    Memory RSS:         no regression > 30% relative (WARN)

  Aggregate mAP alone SHALL NOT authorize deployment.
  Per-class metrics, class confusion, and UNKNOWN preservation must all pass.

Current regression closure: NOT CLOSED.
  - No golden evaluation dataset exists.
  - No accuracy metrics are computed.
  - No comparison framework exists.
  - benchmark_raw.py provides latency data only.
```

---

## A10 — MODEL ROLLBACK

```text
ModelRollbackRule:

Rollback restores an exact artifact by hash:
  ACTIVE model V2 (hash: abc123)
    ↓ failure / regression detected
  PROMOTED model V1 (hash: def456) — the immediate predecessor of V2
    ↓
  re-activate V1 as ACTIVE
  V2 → RETIRED (rollback reason recorded)

Rollback requires:
  modelId (SHA-256)         — exact artifact to restore
  config identity (configId) — config known-compatible with rollback target
  service contract version   — schema version compatible with rollback target
  deployment revision        — audit trail of what was deployed when

Rollback MUST NOT require:
  retraining
  re-evaluation (the rollback target was already PROMOTED)
  code changes

Current rollback readiness:
  NO. Single model. No predecessor. No rollback target exists.
  Rollback capability requires model version history (Phase 4).
```

---

## A11 — MODEL / SERVICE COMPATIBILITY

```text
Compatibility dimensions:

ServiceSchemaCompatibility:
  Determined by: GET /version supportedSchemas ∩ Adapter.SupportedSchemas.
  Current: compatible (both "uniclaw.localVisionEvidence.v1").
  modelId does NOT determine schema compatibility.

ModelRuntimeCompatibility:
  Current service ASSUMES:
    • YOLOv8 architecture (ultralytics YOLO class)
    • PyTorch .pt format
    • Single-file model artifact
    • CPU inference (device="cpu")
  These are hard assumptions in backends.py _get_yolo_model().
  A model in ONNX format or YOLOv5 architecture would fail to load.
  modelId does NOT encode format/architecture — it's a hash, not metadata.

ClassVocabularyCompatibility:
  Current service HAS a hard-coded YOLO_LABEL_ALIASES dict (21 raw → 14 canonical).
  Current label-mapping.json HAS a hard-coded mappings dict (18 entries).
  A model trained with different class indices or label names would produce
  incorrect or empty results.
  modelId does NOT encode class vocabulary.
  Compatibility check: YOLO model.names must map correctly through
  normalize_yolo_label() → label-mapping.json mappings.

ConfigCompatibility:
  configHash identifies the config used. A model trained with config C1
  deployed with config C2 may produce degraded results (different detection
  confidence threshold, different label mappings).
  configHash change should trigger regression evaluation.

modelId is identity/observability, NOT a compatibility decision.
Compatibility requires separate checks:
  • Schema version negotiation (at Host startup)
  • Model format/architecture validation (at model load)
  • Class vocabulary validation (at model load / first inference)
  • Config compatibility (regression evaluation against golden set)
```

---

## A12 — CONFIGURATION INVENTORY

```text
ConfigInventory:

# │ Item                        │ CurrentLocation        │ CurrentDefault │ RuntimeMutable? │ ModelBound? │ DatasetBound? │ DeploymentBound? │ HashIncluded? │ FutureOwner
──┼─────────────────────────────┼────────────────────────┼────────────────┼─────────────────┼─────────────┼───────────────┼──────────────────┼───────────────┼────────────────
1 │ YOLO model path             │ env / server.py:42     │ best.pt path   │ NO (warmup)     │ YES         │ NO            │ YES              │ NO¹           │ DEPLOYMENT
2 │ YOLO imgsz                  │ server.py:55           │ 640            │ NO (warmup)     │ YES         │ NO            │ NO               │ NO            │ MODEL_METADATA
3 │ YOLO confidence threshold   │ label-mapping.json     │ 0.35           │ NO (warmup)     │ NO²         │ NO            │ NO               │ YES           │ VISION_SERVICE
4 │ YOLO device                 │ server.py:96           │ "cpu"          │ NO (warmup)     │ NO          │ NO            │ YES              │ NO            │ DEPLOYMENT
5 │ OCR backend selection       │ env / server.py:51     │ "rapidocr"     │ NO (warmup)     │ NO          │ NO            │ YES              │ NO            │ DEPLOYMENT
6 │ OCR mode (full/roi)         │ env / server.py:60     │ "full"         │ NO (warmup)     │ NO          │ NO            │ YES              │ NO            │ DEPLOYMENT
7 │ OCR text score threshold    │ env / server.py:53     │ 0.5            │ NO (warmup)     │ NO          │ NO            │ YES              │ NO            │ VISION_SERVICE
8 │ OCR language (paddleocr)    │ env / server.py:48     │ "en"           │ NO (warmup)     │ NO          │ NO            │ YES              │ NO            │ DEPLOYMENT
9 │ OCR parallel workers        │ env / backends.py:352  │ 4              │ NO (warmup)     │ NO          │ NO            │ YES              │ NO            │ DEPLOYMENT
10│ OMP threads                 │ env / server.py:7      │ 4              │ NO (import time)│ NO          │ NO            │ YES              │ NO            │ DEPLOYMENT
11│ Image max width (preprocess)│ label-mapping.json     │ 720            │ NO (warmup)     │ NO          │ NO            │ NO               │ YES           │ VISION_SERVICE
12│ Image crop top ratio        │ label-mapping.json     │ 0.0625         │ NO (warmup)     │ NO          │ NO            │ NO               │ YES           │ VISION_SERVICE
13│ Image crop bottom ratio     │ label-mapping.json     │ 0.0625         │ NO (warmup)     │ NO          │ NO            │ NO               │ YES           │ VISION_SERVICE
14│ Label mappings (21→14)      │ backends.py:14-38      │ hard-coded     │ NO (warmup)     │ YES         │ NO            │ NO               │ NO¹           │ MODEL_METADATA
15│ Label → type mappings       │ label-mapping.json     │ 18 entries     │ NO (warmup)     │ NO²         │ NO            │ NO               │ YES           │ VISION_SERVICE
16│ Interactive label set       │ fusion.py:9-26         │ 16 labels      │ NO (warmup)     │ NO          │ NO            │ NO               │ NO¹           │ VISION_SERVICE
17│ Non-item labels             │ label-mapping.json     │ ["popup","img"]│ NO (warmup)     │ NO          │ NO            │ NO               │ YES           │ VISION_SERVICE
18│ ROI padding spec            │ label-mapping.json     │ x:0.15,y:0.10..│ NO (warmup)     │ NO          │ NO            │ NO               │ YES           │ VISION_SERVICE
19│ Edge threshold (scroll)     │ label-mapping.json     │ 0.92           │ NO (warmup)     │ NO          │ NO            │ NO               │ YES           │ VISION_SERVICE
20│ Fusion max OCR distance     │ fusion.py              │ 0.055×diag    │ NO (warmup)     │ NO          │ NO            │ NO               │ NO¹           │ VISION_SERVICE
21│ Chevron row tolerance       │ fusion.py:296          │ 40px           │ NO (warmup)     │ NO          │ NO            │ NO               │ NO¹           │ VISION_SERVICE
22│ Socket path                 │ uvicorn --uds / env    │ /tmp/...sock   │ NO (process arg)│ NO          │ NO            │ YES              │ NO            │ PROVIDER_HOST

¹ Not included in configHash (configHash = SHA-256 of label-mapping.json only).
² Can be model-bound if model was trained with specific confidence threshold.
```

### A12.1 Environment variable overrides

Items also overridable via UNICLAW_* env vars: 1, 5-10, 11-13.
Env var takes precedence over JSON config. This is deliberate for deployment
flexibility but creates drift risk — same configHash, different effective
behavior if env vars differ.

---

## A13 — CONFIG OWNERSHIP

```text
ConfigOwnership:

Current state: ACCEPTABLE with minor drift pressure.

The ONE AUTHORITATIVE OWNER principle is mostly followed:
  • label-mapping.json is the single source for: label→type mappings, spatial
    config (preprocessing, roiPadding, edgeThreshold), detection confidence.
  • YOLO_LABEL_ALIASES in backends.py is separately owned but has different
    semantics (raw YOLO label → canonical label, not canonical → Runtime type).
  • Fusion constants (max_ocr_distance_ratio, max_y_delta_px) are hard-coded
    in fusion.py with no config file counterpart.

Drift pressures identified:
  1. Items 11-13 (preprocessing) can be overridden by env vars with different
     defaults from label-mapping.json. If env var is set differently in
     production vs. evaluation, configHash does not capture the difference.
  2. Items 20-21 (fusion constants) are hard-coded. Changes require code change.
     Should be in label-mapping.json or a separate fusion config.
  3. Item 4 (YOLO device) is hard-coded in server.py:96 ("cpu"). Overridable
     only by code change, not env var.

Recommendations (Phase 3/4, not Phase 2):
  • Extract fusion constants (20-21) into label-mapping.json or fusion config.
  • Document env var precedence in CONTRACT.md.
  • Consider inclusion of env-var-influenced parameters in a future
    EffectiveConfigManifest that captures the actual runtime configuration.
```

---

## A14 — CONFIG VERSIONING

```text
ConfigHashCompleteness: PARTIAL

Current configHash = SHA-256(label-mapping.json file contents).

label-mapping.json truthfully captures:
  ✓ label→type mappings (items 15, 17)
  ✓ spatial preprocessing (items 11-13)
  ✓ ROI padding (item 18)
  ✓ edge threshold (item 19)
  ✓ detection confidence (item 3)

label-mapping.json DOES NOT capture:
  ✗ YOLO imgsz (item 2) — hard-coded 640 in server.py
  ✗ YOLO device (item 4) — hard-coded "cpu" in server.py
  ✗ OCR text score threshold (item 7) — env var, default 0.5
  ✗ OCR parallel workers (item 9) — env var, default 4
  ✗ Fusion max OCR distance (item 20) — hard-coded 0.055 in fusion.py
  ✗ Chevron row tolerance (item 21) — hard-coded 40px in fusion.py
  ✗ Interactive label set (item 16) — hard-coded in fusion.py
  ✗ YOLO_LABEL_ALIASES (item 14) — hard-coded in backends.py
  ✗ Env var overrides that change effective behavior from JSON defaults

Items NOT captured can change perception output WITHOUT configHash change.
This makes configHash PARTIAL, not MISLEADING — it truthfully represents
the config file, but the config file is not the complete effective config.

A consumer seeing identical configHash across two runs might incorrectly
assume identical configuration. The env var overrides (items 11-13) and
hard-coded constants (items 20-21) could differ.
```

---

## A15 — EFFECTIVE CONFIG MANIFEST

```text
EffectiveConfigManifest (proposal, future Phase 3/4):

{
  "schemaVersion": "uniclaw.effectiveConfig.v1",
  "labelMapping": {
    "version": "1.0",
    "hash": "sha256:abc123..."
  },
  "yolo": {
    "imgsz": 640,
    "confidence": 0.35,
    "device": "cpu"
  },
  "ocr": {
    "backend": "rapidocr",
    "mode": "full",
    "textScoreThreshold": 0.5,
    "parallelWorkers": 4
  },
  "fusion": {
    "maxOcrDistanceRatio": 0.055,
    "chevronRowTolerancePx": 40,
    "interactiveLabels": ["button", "list_item", "toggle", ...],
    "promoteUnmatchedOcr": true
  },
  "preprocessing": {
    "maxWidth": 720,
    "cropTopRatio": 0.0625,
    "cropBottomRatio": 0.0625
  },
  "model": {
    "reference": "android_ui_detection_yolov8",
    "classVocabulary": ["text_block", "switch", "icon", ...]
  },
  "pipeline": {
    "name": "local-vision",
    "version": "1.0"
  }
}

Then:
  configId = SHA-256(canonical JSON serialization of effective config manifest)

Canonical serialization: sorted keys, no whitespace, UTF-8.
This captures all parameters that affect perception output.
MODEL ARTIFACT (modelId) is separate from INFERENCE CONFIG (configId).
DEPLOYMENT CONFIG (socket, device, workers) is also separate.

Current: NOT implemented. Phase 4 governance work.
```

---

## A16 — CONFIG CHANGE LIFECYCLE

```text
ConfigLifecycleTarget (future, Phase 4):

States:
  DRAFT       → config change proposed
  VALIDATED   → change is syntactically valid, all referenced labels exist
  REGRESSION  → evaluated against golden dataset, compared with ACTIVE config
  APPROVED    → regression passed + human/governance approval
  ACTIVE      → deployed to production vision service
  OBSERVE     → monitoring in production (drift detection, evidence quality)
  ROLLBACK    → revert to previous ACTIVE config

A config change that can alter perception evidence MUST pass the same
regression corpus as a model change.

Examples of config changes that REQUIRE regression:
  • confidence threshold change (item 3): affects which detections survive
  • class mapping change (item 15): "switch" → "toggle" → "button" changes
    what Runtime sees as interactive
  • fusion threshold change (item 20): affects OCR→YOLO matching
  • interactive label set change (item 16): affects which elements appear
    as candidates

Examples of config changes that do NOT require regression:
  • Socket path change (item 22): deployment concern
  • OCR parallel workers (item 9): performance, not correctness
  • OMP threads (item 10): performance, not correctness

"Only config changed" is NOT a bypass for evaluation.
```

---

## A17 — CONFIG DRIFT DETECTION

```text
ConfigDriftDetection:

Current (Phase 1):
  configHash included in:
    • GET /version response
    • Every /v1/analyze response metadata

  No consumer currently reads configHash to detect drift.
  Drift detection is infrastructure-ready but not activated.

Expected observability relationship:
  ExpectedConfigId (from deployment manifest)
       ↕
  Service /version configHash / configId

  Drift → OPERATIONAL WARNING / DIAGNOSTIC.
  Drift MUST NOT become:
    • semantic failure
    • Agent decision
    • GoalEvidence mutation
    • Runtime state change

Future recording locations:
  • TraceRun: optional attribute on environment.observe span
  • CaptureSession: perception metadata block
  • Deployment metadata: PerceptionDeployment record (see §A18)
  • Dedicated PerceptionManifest: if cross-run comparison is needed

  Harness records drift. Harness does NOT control Runtime.
```

---

## A18 — MODEL + CONFIG DEPLOYMENT UNIT

```text
PerceptionDeploymentIdentity (proposal):

{
  "perceptionDeployment": {
    "service": "local-vision@1.0",
    "schema": "uniclaw.localVisionEvidence.v1",
    "modelId": "android_ui_detection_yolov8/abc123def456",
    "configId": "sha256:789abc..."
  }
}

This identity allows any recorded perception result to answer:
  "Which exact service/model/config combination produced this evidence?"

Current (Phase 1):
  All four fields are available:
    service:   /version serviceVersion
    schema:    /version supportedSchemas + /v1/analyze metadata.schema
    modelId:   /version modelId
    configHash: /version configHash + /v1/analyze metadata.configHash

  But configHash is PARTIAL (see §A14). Until configId replaces configHash,
  the deployment identity is incomplete.

Recording in Trace/Replay assets:
  • CaptureSession can record perceptionDeployment at session start.
  • TraceRun can record perceptionDeployment as span attribute on
    environment.observe.
  • No Runtime semantic change required — these are Harness-owned artifacts.
```

---

## A19 — EVIDENCE PROVENANCE CLOSURE

```text
EvidenceProvenanceClosure:

Current lineage (what IS recorded):
  Screenshot / Frame
    → (no deployment identity recorded)
    → Raw detection  (YOLO: id, label, confidence, bounds)
    → OCR            (token: id, text, confidence, bounds)
    → Fusion         (candidate: id, type, text, confidence, bounds, evidence refs, riskFlags)
    → Normalized     (bounds in [0,1]×[0,1] full-screenshot space)
    → Observation    (ObservedElement[] via C# adapter)

Missing provenance links:
  • modelId: recorded in /v1/analyze metadata.models.yolo — available but not
    persisted into Trace/Capture assets.
  • configHash: recorded in /v1/analyze metadata.configHash — available but
    partial (see §A14).
  • serviceVersion: recorded in /v1/analyze metadata.pipeline.version.
  • EffectiveConfigId: NOT YET COMPUTED (requires §A15 manifest).

Future target:
  Every persisted perception artifact should be reproducible against:
    • exact model artifact (modelId)
    • exact effective config (configId, once implemented)
    • exact pipeline/service version

  Missing provenance stays UNKNOWN.
  Never infer version from filename, path, or mtime.
```

---

## A20 — FAILURE AND DRIFT BEHAVIOR

```text
Audit of failure scenarios:

│ Scenario                      │ Current behavior                          │ Desired outcome      │
├───────────────────────────────┼───────────────────────────────────────────┼───────────────────────
│ Model missing                 │ server.py: warmup_yolo → FileNotFoundError│ STARTUP_BLOCK         │
│                               │ → lifespan crashes → server fails to start│                       │
│ Model corrupted               │ warmup_yolo → ultralytics load error      │ STARTUP_BLOCK         │
│                               │ → lifespan crashes                        │                       │
│ Wrong model (different arch)  │ warmup_yolo → ultralytics load error      │ STARTUP_BLOCK         │
│                               │ or predict fails at first request          │ or REQUEST_FAILURE    │
│ Config missing                │ _load_spatial → FileNotFoundError         │ STARTUP_BLOCK         │
│ Config malformed              │ _load_spatial → JSONDecodeError           │ STARTUP_BLOCK         │
│ Config changed unexpectedly   │ _load_spatial → new configHash            │ WARNING (drift)       │
│                               │ (currently: no consumer detects drift)    │                       │
│ Unsupported class mapping     │ normalize_yolo_label → passes unknown     │ WARNING               │
│                               │ label through. label-mapping.json lookup  │ (unknown label →      │
│                               │ fails → candidate type = raw label.       │  UNKNOWN type)        │
│ Model/config mismatch         │ Not detected. Service starts normally.    │ WARNING               │
│                               │ May produce degraded results.             │ (regression gate      │
│                               │                                           │  catches at promotion)│
│ Service/schema mismatch       │ /version intersection empty               │ STARTUP_BLOCK         │
│                               │ (Phase 2 Host)                            │                       │
│ Request timeout               │ FastAPI request timeout → 500             │ REQUEST_FAILURE       │
│                               │ Adapter: empty array (fail closed)        │ → EMPTY_EVIDENCE      │
│ Malformed image bytes         │ PIL.Image.open raises → 500               │ REQUEST_FAILURE       │
│                               │ Adapter: empty array                      │ → EMPTY_EVIDENCE      │
│ Inference exception           │ unhandled → 500                           │ REQUEST_FAILURE       │
│                               │ Adapter: empty array                      │ → EMPTY_EVIDENCE      │

NO failure path fabricates positive perception evidence.
All paths → empty candidates or startup block.
Fail-closed, truthful. VERIFIED.
```

---

## A21 — CONCURRENCY / MUTABLE STATE

```text
Concurrency audit:

Module globals (process-level singletons):
  backends._yolo_model_cache       dict          — YOLO model cache. Thread-safe (read-only after load).
  backends._rapid_ocr_singleton    object        — RapidOCR instance. Thread-safe by design (D-198).
  backends._rapid_ocr_lock         threading.Lock — Guards singleton creation. Correct double-check.
  backends._ocr_executor           ThreadPoolExecutor — Shared thread pool. Thread-safe.
  backends._ROI_PADDING_SPEC       dict          — Written once at startup, read-only thereafter.
  server._SPATIAL                  dict          — Written once at startup, read-only thereafter.
  server._DETECTION_CONF           float         — Written once at startup, read-only thereafter.
  server._CONFIG_HASH              str           — Written once at startup, read-only thereafter.
  server._MAX_WIDTH / _CROP_TOP / _CROP_BOTTOM — Written once, read-only thereafter.
  server._WARM                     bool          — Written during lifespan, read by /health.
                                                  Single-writer (lifespan), concurrent reads safe.

Per-request state:
  All per-request state is local variables within _run_pipeline.
  No shared mutable state across requests.
  Images decoded per-request, garbage collected.

Production assumptions:
  SINGLE_PROCESS:  REQUIRED. Module-level singletons are not shared across processes.
                   Multiple uvicorn workers would each load their own model copy
                   (6.2 MB × N workers = memory explosion).
  SINGLE_WORKER:   REQUIRED. YOLO/OCR inference is synchronous and blocks the
                   event loop. Multiple workers would serialize on the GIL for
                   CPU inference but could interleave I/O. Current single-worker
                   topology is correct for CPU-bound inference workload.

Phase 2 constraint: FREEZE single-process, single-worker.
Do NOT enable --workers > 1 without:
  • Proof that model memory sharing works across workers.
  • Proof that concurrent inference does not degrade latency.
  • Measured memory budget for N workers.
```

---

## A22 — PYTHON DEPENDENCY GOVERNANCE

```text
Dependency inventory (from requirements.txt):

│ Package                     │ Version  │ Classification     │ Notes                           │
├─────────────────────────────┼──────────┼────────────────────┼─────────────────────────────────
│ torch                       │ 2.2.2    │ YOLO_RUNTIME       │ Last Intel-macOS wheels         │
│ torchvision                 │ 0.17.2   │ YOLO_RUNTIME       │ Required by ultralytics          │
│ ultralytics                 │ 8.4.115  │ YOLO_RUNTIME       │ YOLO inference engine            │
│ rapidocr-onnxruntime        │ 1.4.4    │ OCR_RUNTIME        │ Default OCR backend (D-198)      │
│ onnxruntime                 │ 1.23.2   │ OCR_RUNTIME        │ ONNX inference engine            │
│ paddleocr                   │ 2.10.0   │ OCR_RUNTIME_LEGACY │ Legacy fallback (memory leak)    │
│ paddlepaddle                │ 3.0.0    │ OCR_RUNTIME_LEGACY │ Paddle inference engine          │
│ pillow                      │ ≥10.0.0  │ RUNTIME_REQUIRED   │ Image decode/manipulation        │
│ numpy                       │ 1.26.4   │ RUNTIME_REQUIRED   │ Array operations                 │
│ opencv-python               │ 4.10.0.84│ LEGACY_UNUSED?     │ May not be needed (PIL used)     │
│ opencv-contrib-python       │ 4.10.0.84│ LEGACY_UNUSED?     │ May not be needed                │
│ opencv-python-headless      │ 4.10.0.84│ LEGACY_UNUSED?     │ May not be needed                │
│ fastapi                     │ 0.141.1  │ RUNTIME_REQUIRED   │ HTTP framework                   │
│ uvicorn[standard]           │ 0.52.1   │ RUNTIME_REQUIRED   │ ASGI server                      │
│ pytest                      │ ≥8       │ DEV_TEST           │ Test framework                   │

Dependency strategy:
  Python version:        3.11 (pinned — Intel macOS compatibility)
  requirements.txt:      Exact pins for all runtime packages.
                         torch/torchvision pinned for Intel-macOS wheel availability.
  Environment:           .venv-local-vision (dedicated venv).
  Reproducibility:       requirements.txt + Python 3.11 + Intel macOS = reproducible.
                         Not portable to Apple Silicon without torch version bump.
  Offline installation:  NOT supported. pip install requires network.
                         Deployment must pre-provision .venv.

Host MUST NOT:
  • pip install (deployment concern)
  • create venv (deployment concern)
  • download models (deployment concern)
  • train models (training concern)

Host MAY:
  • Validate Python version ≥ 3.11
  • Validate packages are importable
  • Validate model file exists and non-empty
  • Validate config file exists and is valid JSON
```

---

## A23 — TRAINING ASSET DISCOVERY

```text
TrainingAssets: ABSENT

Search results:
  • No training scripts (*.py with train/fine-tune/epoch/learn).
  • No dataset definitions (no YAML/JSON with train/val splits).
  • No annotation files (no bounding-box annotation format found).
  • No augmentation configuration.
  • No class definition files beyond YOLO_LABEL_ALIASES in backends.py.
  • No export scripts (model format conversion: .pt → .onnx, etc.).
  • No checkpoint handling.

  The current model (best.pt) is externally sourced from the Deki-Yolo project.
  No in-repo training capability exists.

Classification per asset:
  Training scripts:        ABSENT
  Dataset definitions:     ABSENT
  Annotations:             ABSENT
  Augmentation config:     ABSENT
  Evaluation scripts:      PARTIAL (benchmark_raw.py is latency only)
  Export scripts:          ABSENT
  Checkpoint handling:     ABSENT
```

---

## A24 — MODEL TRAINING CLOSED LOOP TARGET

```text
Desired future closed loop (governance model only — no implementation authorized):

Reality Failure / Vision Regression Evidence
        ↓
Dataset Candidate
  └─ Screenshots where perception produced incorrect evidence
  └─ Screenshots where model missed expected detections
  └─ Screenshots with new UI patterns not in training set
        ↓
Annotation
  └─ Human annotates ground truth (bounds, labels, text, switch states)
  └─ Review workflow (challenged → corrected → consensus)
        ↓
Dataset Version
  └─ Immutable snapshot: settings-chinese-rom/2026-08-12
  └─ Split: train / val / test
        ↓
Training Run
  └─ Fine-tune YOLO on new dataset version
  └─ Record: training config, code revision, dataset version, metrics
        ↓
Candidate Model
  └─ artifact_hash = SHA-256(best.pt)
  └─ semantic_version assigned (human decision)
        ↓
Evaluation
  └─ Run on golden evaluation dataset
  └─ Compute: mAP@0.5, per-class precision/recall, class confusion
        ↓
Regression against ACTIVE
  └─ Same golden dataset, same metrics
  └─ Apply regression thresholds (see §A9)
        ↓
Promotion
  └─ All gates pass → PROMOTED
  └─ Human/governance approval recorded
        ↓
Deployment
  └─ Model artifact placed in deployment location
  └─ PerceptionDeployment updated with new modelId
  └─ Service restarted (Host handles this)
        ↓
Reality Observation
  └─ Production evidence recorded with modelId + configId
  └─ Evidence quality monitoring
  └─ Failures → back to Dataset Candidate

This is a governance model. Training orchestration is Phase 4+.
```

---

## A25 — TEST / EVALUATION LAYERS

```text
Current layers:

LEVEL 1 — Unit tests
  tests/test_server.py          — 13 test methods across 5 classes
  tests/test_fusion.py          — fusion logic tests
  tests/test_backends_fusion.py — backend integration tests
  GATES: code change (PR), config change (label mapping logic)

LEVEL 2 — Python pipeline tests
  tests/test_server.py          — mocked YOLO/OCR, real fusion + serialization
  GATES: code change, fusion change, schema change

LEVEL 3 — Known-image perception regression
  benchmark_raw.py              — latency only, NOT accuracy
  NO accuracy regression exists
  GATES: SHOULD GATE config change, model change (currently MISSING)

LEVEL 4 — Reality corpus replay
  Existing evidence JSON files in artifacts/local-vision/
  Can be replayed through C# adapter for integration testing.
  GATES: SHOULD GATE model promotion, config promotion (currently INFORMAL)

LEVEL 5 — Live emulator calibration
  NOT YET IMPLEMENTED
  GATES: WOULD GATE service release, deployment validation

LEVEL 6 — Runtime semantic golden run
  Existing Runtime regression: 819/819 PASS
  GATES: Runtime release (existing), perception deployment change (new)

Layer gating matrix (target):

│ Change type      │ L1  │ L2  │ L3      │ L4      │ L5  │ L6      │
├──────────────────┼─────┼─────┼─────────┼─────────┼─────┼─────────
│ Code change      │ REQ │ REQ │ REC     │ REC     │ —   │ REQ     │
│ Config change    │ REQ │ REQ │ REQ     │ REC     │ —   │ REQ     │
│ Model change     │ REQ │ REQ │ REQ     │ REQ     │ REC │ REQ     │
│ Service release  │ REQ │ REQ │ REQ     │ REQ     │ REQ │ REQ     │

REQ = Required (must pass before merge/deploy)
REC = Recommended (should pass; human judgment if not)
—   = Not applicable

Current gaps:
  L3: NO accuracy regression suite exists → BLOCKER for model promotion
  L4: Evidence replay is informal → needs formalization for model promotion
  L5: Live emulator calibration not implemented → Phase 4
```

---

## A26 — P4 CLOSURE

```text
P4: modelId changes when model content changes.

Strategy: Temp-file copy approach.

Procedure:
  1. Copy production model to temp location.
     cp artifacts/local-vision/models/android_ui_detection_yolov8/best.pt \
        /tmp/uniclaw-p4-test-{uuid}/best.pt

  2. Launch vision service with UNICLAW_YOLO_MODEL={temp_path}.
     (Service will fail to infer with modified model, but modelId is
      computed from SHA-256 at _model_id() call during GET /version,
      which reads the file bytes, not loads the model.)

  3. Query GET /version. Record modelId_1.

  4. Shut down service.

  5. Modify one byte in temp model file:
     with open(temp_path, "r+b") as f:
         f.seek(0)
         current = f.read(1)
         f.seek(0)
         f.write(b'\x00' if current != b'\x00' else b'\x01')

  6. Launch vision service with same UNICLAW_YOLO_MODEL={temp_path}.

  7. Query GET /version. Record modelId_2.

  8. Assert: modelId_1 ≠ modelId_2.

  9. Assert: modelId_1 matches expected (SHA-256 of original model, first 12 chars).

  10. Clean up temp directory.

Production model is NEVER modified. Only the temp copy is mutated.
The test proves:
  • modelId is a deterministic function of model file contents.
  • Any change to model bytes → different modelId.
  • Original model retains its identity.

P4: PASS  (strategy defined; execution deferred to Phase 2 implementation)
```

---

## A27 — MIGRATION PLAN

```text
Future mapping: tools/local_vision/* → platforms/perception/*

│ Current                                     │ Target                                          │ Action              │
├─────────────────────────────────────────────┼─────────────────────────────────────────────────┼─────────────────────
│ tools/local_vision/server.py                │ platforms/perception/vision-service/server.py   │ MOVE_AND_REFACTOR   │
│                                             │                                                 │ (Phase 2: socket    │
│                                             │                                                 │  injection via env, │
│                                             │                                                 │  SIGTERM handling)  │
│ tools/local_vision/backends.py              │ platforms/perception/vision-service/backends.py │ MOVE_AS_IS          │
│ tools/local_vision/fusion.py                │ platforms/perception/vision-service/fusion.py   │ MOVE_AS_IS          │
│ tools/local_vision/schema.py                │ platforms/perception/vision-service/schema.py   │ MOVE_AS_IS          │
│ tools/local_vision/analyze.py               │ platforms/perception/vision-service/analyze.py  │ MOVE_AS_IS          │
│ tools/local_vision/label-mapping.json       │ platforms/perception/config/label-mapping.json  │ MOVE_AS_IS          │
│ tools/local_vision/requirements.txt         │ platforms/perception/vision-service/            │ MOVE_AS_IS          │
│                                             │   requirements.txt                              │                     │
│ tools/local_vision/benchmark_raw.py         │ platforms/perception/evaluation/                │ MOVE_AS_IS          │
│                                             │   benchmark_raw.py                              │                     │
│ tools/local_vision/tests/test_server.py     │ platforms/perception/tests/test_server.py       │ MOVE_AS_IS          │
│ tools/local_vision/tests/test_fusion.py     │ platforms/perception/tests/test_fusion.py       │ MOVE_AS_IS          │
│ tools/local_vision/tests/test_backends_fusion.py│ platforms/perception/tests/                 │ MOVE_AS_IS          │
│                                             │   test_backends_fusion.py                       │                     │
│ tools/local_vision/tests/__init__.py        │ platforms/perception/tests/__init__.py          │ MOVE_AS_IS          │
│ tools/local_vision/__init__.py              │ platforms/perception/vision-service/__init__.py │ MOVE_AS_IS          │
│ —                                           │ platforms/perception/CONTRACT.md                │ Phase 1: DONE       │
│ —                                           │ platforms/perception/host/provider_host.py      │ Phase 2: CREATE     │
│ —                                           │ platforms/perception/host/version_negotiation.py│ Phase 2: CREATE     │
│ —                                           │ platforms/perception/models/yolo/.../model_card.md│ Phase 4: CREATE    │
│ artifacts/local-vision/models/.../best.pt   │ platforms/perception/models/yolo/.../best.pt   │ MOVE_AS_IS (Phase 3)│
│ artifacts/local-vision/models/yolo11n.pt    │ platforms/perception/models/yolo/yolo11n.pt    │ DEPRECATE or MOVE   │

Migration NOT executed. This is the plan only.
```

---

## A28 — PHASE 2 HOST PREREQUISITES

```text
HostPrerequisiteFixes:

Identified changes required BEFORE or DURING Phase 2 Host implementation:

FIX 1 — Socket path injection                    [PHASE_2_MINIMAL_FIX]
  server.py: Read UNICLAW_VISION_SOCKET env var.
  Use: logging the socket path for diagnostics.
  uvicorn --uds already handles binding. The env var is for the service
  to know its own socket path (for log messages, health self-check).
  Impact: 2 lines added to lifespan startup logging.
  Backward-compatible: if env var absent, no change in behavior.

FIX 2 — Graceful SIGTERM handling               [PHASE_2_MINIMAL_FIX]
  Current: uvicorn handles SIGTERM. server.py lifespan shutdown yields.
  No custom cleanup needed (no open files, no shared memory).
  Impact: NONE. Already handled by uvicorn. VERIFIED READY.

FIX 3 — Explicit model/config path injection    [READY_AS_IS]
  Current: UNICLAW_YOLO_MODEL and UNICLAW_LABEL_MAPPING env vars already
  supported. Host passes them to child process.
  Impact: NONE. Already implemented. VERIFIED READY.

FIX 4 — Readiness semantics                     [READY_AS_IS]
  Current: GET /health warm=true proves YOLO loaded + OCR initialized.
  Sufficient for Phase 2 Host readiness determination.
  Pipeline smoke test (real image inference during warmup) is Phase 4.
  Impact: NONE for Phase 2.

FIX 5 — Version endpoint                        [READY_AS_IS — Phase 1]
  GET /version already implemented (Phase 1).
  Returns supportedSchemas, serviceVersion, modelId, configHash.
  Impact: NONE. Already done.

FIX 6 — UDS path clean exit                     [READY_AS_IS]
  Current: uvicorn does NOT clean up --uds socket file on exit.
  Phase 2 Host owns socket cleanup (see REVIEW 4).
  Impact: NONE for server.py. Host responsibility.

Summary:
  Phase 2 requires ONE minimal change to server.py:
    Read UNICLAW_VISION_SOCKET for diagnostic logging.

  All other Host prerequisites are already satisfied or are Host-side
  responsibilities with no server.py changes needed.

  NO Phase 3 refactors. NO Phase 4 governance work.
```

---

## A29 — ARCHITECTURE AUTHORITY CHECK

```text
SemanticAuthorityLeak: NONE

Python Perception Platform owns:
  ✓ Image interpretation mechanisms (YOLO, OCR, fusion)
  ✓ Model execution
  ✓ Perception configuration (label mapping, thresholds, preprocessing)
  ✓ Model/config lifecycle (Phase 4 governance)

Python Perception Platform does NOT own:
  ✗ BusinessIntent          — not referenced in any Python file
  ✗ SemanticGoal            — not referenced
  ✗ SemanticAction          — not referenced
  ✗ Capability selection    — not referenced (type labels are evidence, not selection)
  ✗ DeviceAction            — not referenced
  ✗ Agent decision          — not referenced
  ✗ Container belief        — not referenced
  ✗ GoalEvidence            — not referenced
  ✗ Task completion         — not referenced
  ✗ Runtime recovery        — not referenced

Boundary enforcement:
  • /v1/analyze output contains ONLY structured evidence (candidates, detections,
    OCR tokens, scroll hints, metadata).
  • No action decisions ("tap this"), semantic goals, or capability selections
    in output.
  • scrollHints is raw observable (counts, positions), not a scroll decision.
  • search-box pre-labeling and chevron heuristic are perception-internal
    reclassifications within the candidate type vocabulary. They do not
    prescribe Runtime behavior.
  • C# adapter translates candidates → ObservedElement with type normalization
    but no semantic inference.

VERIFIED: No authority leak. Perception Platform is cleanly below IEnvironment.
```

---

## A30 — CLOSED-LOOP READINESS

```text
ClosedLoopReadiness: 10-point assessment

1. Which exact model produced this evidence?
   PARTIAL — modelId available via GET /version and metadata.models.yolo
   in /v1/analyze output. But modelId is NOT yet persisted into Trace/Capture
   assets by the Harness. Available at service boundary, not in evidence lineage.

2. Which exact configuration produced this evidence?
   PARTIAL — configHash available but PARTIAL (see §A14). Does not capture
   fusion constants, YOLO imgsz, env var overrides. configId not yet computed.

3. Which service/pipeline version produced it?
   YES — serviceVersion in /version. metadata.pipeline.version = "1.0" in
   /v1/analyze output. Service is single-version (no release history to confuse).

4. Which dataset trained the model?
   NO — model is externally sourced (Deki-Yolo). No training dataset recorded.
   model_card.md not yet created. Training provenance absent.

5. Which evaluation admitted the model?
   NO — no evaluation framework exists. Model is ACTIVE by convention
   (best.pt in artifacts/). No evaluation metrics, no regression comparison,
   no promotion decision.

6. Which model/config combination is currently ACTIVE?
   PARTIAL — can be determined by querying GET /version on the running service.
   But no deployment manifest records it persistently. If service is down,
   ACTIVE identity must be inferred from filesystem artifacts.

7. Can we reproduce a historical perception result?
   PARTIAL — coordinates are deterministic (normalized to full-screenshot frame).
   Evidence JSON files in artifacts/local-vision/ record historical outputs.
   But without exact model artifact (modelId) and exact effective config
   (configId), reproduction may differ if model/config have changed.

8. Can we compare a candidate against ACTIVE?
   NO — no evaluation framework, no golden dataset, no regression suite.
   benchmark_raw.py is latency-only. Comparison requires Phase 4 governance.

9. Can we rollback to the previous known-good deployment?
   NO — single model, no version history, no predecessor tracking.
   Rollback requires model version management (Phase 4).

10. Can a real failure become a dataset/regression candidate?
    NO — no path from Runtime failure → perception dataset.
    Reality screenshots exist but without annotation workflow.
    Failure-to-dataset pipeline requires Phase 4 governance.

Score: 1 YES, 4 PARTIAL, 5 NO.
Closed loop is NOT operational. Phase 2 does not close it.
Phases 3-4 progressively close these gaps.
```

---

## AGGREGATE RESULT

```text
PERCEPTION_PLATFORM_PYTHON_VISION_OPERATIONAL_GOVERNANCE_AND_HOST_READINESS_AUDIT_RESULT

AuditStatus:
  READY_WITH_MINIMAL_FIXES

CurrentPipeline:
  JPEG/RGBA → Preprocess(crop+resize) → YOLO(ultralytics, cpu) →
  RapidOCR(full-image, ONNX) → Fusion(spatial+text+heuristics) →
  CoordinateRemap(full-screen normalized) → JSON

ServiceEntryPoint:
  tools/local_vision/server.py:app  (FastAPI)

LaunchCommand:
  python -m uvicorn tools.local_vision.server:app --uds {socketPath}

SocketConfiguration:
  Current:  /tmp/uniclaw-vision.sock  (fixed)
  Target:   /tmp/uniclaw-vision-{sessionGuid}.sock
  Injection: UNICLAW_VISION_SOCKET env var (Phase 2 minimal fix)

ProcessTopology:
  Single uvicorn worker, single OS process.
  Module-level YOLO/OCR singletons.
  CPU inference blocks event loop during request.
  FREEZE: no multi-worker until proven safe.

ReadinessSemantics:
  GET /health warm=true  → YOLO loaded + OCR initialized.
  Does NOT prove pipeline correctness on real images.
  Sufficient for Phase 2. Pipeline smoke test is Phase 4.

ShutdownSemantics:
  uvicorn handles SIGTERM → graceful shutdown.
  Socket file NOT cleaned up by uvicorn.
  Phase 2 Host owns socket cleanup.

HostPrerequisiteFixes:
  ONE minimal fix: server.py reads UNICLAW_VISION_SOCKET for diagnostics.
  All other prerequisites already satisfied (env var injection, /version,
  /health with warm, model/config path env vars).

ModelIdentity:
  modelId = SHA-256(model file, first 12 chars)
  Canonical: artifact hash. Semantic version: absent.
  "best.pt" is filename convention, not identity.

ModelVersionCurrentState:
  Single model. No version history. No predecessor. No semantic version.
  ACTIVE by convention, not by promotion process.

ModelLifecycleTarget:
  6 states: CANDIDATE → VALIDATED → PROMOTED → ACTIVE → RETIRED / REJECTED.
  ROLLED_BACK as event, not state.

ModelPromotionRule:
  6 gates: artifact identity, dataset provenance, evaluation results,
  regression comparison, schema compatibility, explicit decision.
  "training completed" or "best.pt exists" insufficient.

ModelRollbackRule:
  Restore exact artifact by hash. No retraining required.
  Requires predecessor tracking.

ModelProvenance:
  modelId records artifact identity. Training provenance ABSENT.
  model_card.md not created. Dataset provenance ABSENT.

ConfigInventory:
  22 config items across 5 locations. 12 items in configHash. 7 hard-coded.
  3 env-var-overridable with different defaults.

ConfigOwnership:
  Mostly single-owner. Minor drift: fusion constants hard-coded.
  Env var overrides not captured in configHash.

ConfigHashCompleteness:
  PARTIAL — label-mapping.json is truthful but incomplete.
  7 items that affect output are NOT captured by configHash.

EffectiveConfigManifest:
  Proposed: canonical JSON with sorted keys → configId = SHA-256.
  Separates MODEL ARTIFACT from INFERENCE CONFIG from DEPLOYMENT CONFIG.
  Phase 4 implementation.

ConfigLifecycleTarget:
  DRAFT → VALIDATED → REGRESSION → APPROVED → ACTIVE → OBSERVE → ROLLBACK.
  Config change that affects evidence MUST pass regression corpus.

ConfigDriftDetection:
  Infrastructure ready (configHash in /version and /v1/analyze).
  No consumer activated. Drift → WARNING, not semantic failure.

PerceptionDeploymentIdentity:
  Proposed: { service, schema, modelId, configId }.
  All four fields available or target-state defined.
  Recording in Trace/Capture requires Phase 3/4.

EvidenceProvenanceClosure:
  Partial. modelId + configHash + serviceVersion available at service boundary.
  Not persisted into Trace/Capture assets. EffectiveConfigId not yet computed.

TrainingAssets:
  ABSENT. No training scripts, datasets, annotations, or checkpoint handling.
  Model externally sourced from Deki-Yolo.

DatasetAssets:
  ABSENT. No labeled datasets in repository.

EvaluationAssets:
  PARTIAL. benchmark_raw.py is latency benchmark only. No accuracy regression.
  2 reality screenshots + 3 evidence JSON files exist but no evaluation framework.

RegressionClosure:
  NOT CLOSED. No golden evaluation dataset. No accuracy metrics. No comparison
  framework. Thresholds defined in architecture gate but not yet implemented.

RollbackReadiness:
  NO. Single model. No predecessor. No rollback target.

ClosedLoopReadiness:
  1 YES, 4 PARTIAL, 5 NO of 10 questions.
  Phase 2 does not close the loop. Phases 3-4 progressively close gaps.

Phase2MinimalFixes:
  1. server.py reads UNICLAW_VISION_SOCKET for diagnostic logging.
  (All other prerequisites already satisfied.)

Phase3Refactors:
  • Extract fusion constants into config.
  • Move files to platforms/perception/.
  • Update adapter to use Provider Host.
  • Formalize evidence replay as evaluation layer.

Phase4GovernanceWork:
  • EffectiveConfigManifest + configId.
  • Model lifecycle states + promotion rule implementation.
  • Golden evaluation dataset creation.
  • Regression suite (accuracy, not just latency).
  • model_card.md for each model version.
  • Dataset versioning + annotation provenance tooling.
  • Training pipeline (if in-house fine-tuning desired).
  • Config lifecycle + drift detection activation.
  • PerceptionDeploymentIdentity persistence.
  • Closed-loop failure → dataset pipeline.

SemanticAuthorityLeak:
  NONE

P4:
  PASS (strategy defined; execution deferred)

ProviderHostImplementationReady:
  YES — ONE minimal fix required (UNICLAW_VISION_SOCKET read).
  All other Host prerequisites already satisfied.

RuntimeDeltaRequired:
  NONE

ArchitectureDeltaRequired:
  NONE

RecommendedNextTask:
  PROJECT_LEADER_PERCEPTION_PLATFORM_PHASE_2_IMPLEMENTATION
  (authorize Provider Host implementation with Phase 2 constraints)
```

STOP.
