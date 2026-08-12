# Perception Platform Architecture Gate

> Date: 2026-08-12
> Role: Project Leader / Architecture Gate Author
> Lane: `ARCHITECTURE_DISCOVERY`
> Baseline: `CORE_RUNTIME_SEMANTIC_SPINE_GRADUATED` + `RUNTIME_OBSERVABILITY_TRACE_GRADUATED`
> Result: `PERCEPTION_PLATFORM_ARCHITECTURE_GATE_RESULT`
> Implementation authority: **NOT GRANTED**
> Architecture purchase: **GATE DEFINED — NOT PURCHASED**

## 0. Gate mandate

The Python Vision stack (`uni-claw/tools/local_vision`) is an **owned capability
domain**, not an external black box. It contains YOLO object detection, RapidOCR
text recognition, spatial fusion, label mapping, image preprocessing, and
scroll-hint extraction — all under project source control.

This gate defines the long-term perception platform boundary that:
- Preserves Runtime spine purity (no Python, no YOLO, no OCR, no model files).
- Formalizes perception as a first-class owned platform with versioned contracts.
- Defines model/dataset governance for the ML lifecycle.
- Defines the Provider Host contract for lifecycle management.
- Charts migration from the current `tools/local_vision` to a formal platform.

No implementation is authorized by this gate.

---

## 1. Architecture layers

```text
┌─────────────────────────────────────────────────────────┐
│  RUNTIME SPINE (frozen)                                 │
│  Agent → Container → Traversal → Environment            │
│                                                         │
│  Runtime consumes ONLY:                                 │
│    • Perception evidence (ObservedElement[])            │
│    • Normalized candidates (via IEnvironment)           │
│    • Bounds (ElementBounds, normalized [0,1]×[0,1])     │
│    • Text (string, OCR-derived)                         │
│    • Frame-scoped observations (Observation)            │
│                                                         │
│  Runtime MUST NOT depend on:                            │
│    • Python runtime or any Python library               │
│    • YOLO / ultralytics                                 │
│    • OCR libraries (PaddleOCR, RapidOCR, ONNX Runtime)  │
│    • Model files (.pt, .onnx, .pth)                     │
│    • Image preprocessing libraries (PIL, OpenCV)        │
│    • Vision service transport (HTTP, UDS, gRPC)          │
│    • Label mapping configuration                        │
│    • Fusion algorithm details                           │
│    • Any perception-internal type or schema             │
└───────────────────────┬─────────────────────────────────┘
                        │
                        │  IEnvironment  (frozen Runtime port)
                        │  ObserveAsync() → Observation
                        │  ExecuteAsync() → ActionResult
                        │
┌───────────────────────┴─────────────────────────────────┐
│  RUNTIME ADAPTERS (UniClaw.Runtime.Adapters)             │
│                                                         │
│  PhysicalEnvironment : IEnvironment                     │
│    Composes: IScreenshotSource                          │
│            + IPerceptionSource                          │
│            + IAdbDispatchTarget                         │
│            + ISwitchStateReader (frame-scoped)           │
│                                                         │
│  Adapter-private interfaces (NOT Runtime ports):         │
│    IScreenshotSource — screenshot capture               │
│    IPerceptionSource  — perception invocation           │
│    IAdbDispatchTarget — ADB dispatch                     │
│                                                         │
│  Owns: transport mechanics ONLY                         │
│  Owns: NO semantic belief, Agent authority, state truth │
└───────────────────────┬─────────────────────────────────┘
                        │
                        │  Perception Service Contract
                        │  (versioned, language-agnostic)
                        │
┌───────────────────────┴─────────────────────────────────┐
│  PERCEPTION PLATFORM (owned capability domain)           │
│                                                         │
│  ┌───────────────────────────────────────────────────┐  │
│  │ Vision Service (Python FastAPI)                   │  │
│  │   POST /v1/analyze  — image → structured evidence │  │
│  │   GET  /health      — liveness + warm status      │  │
│  │                                                   │  │
│  │   Pipeline: YOLO → OCR → Fusion → Candidates      │  │
│  │   Output:  JSON schema v1 (uniclaw.localVision)   │  │
│  └───────────────────────────────────────────────────┘  │
│                                                         │
│  ┌───────────────────────────────────────────────────┐  │
│  │ Provider Host                                     │  │
│  │   Startup, health, shutdown, crash recovery       │  │
│  │   Version compatibility negotiation               │  │
│  │   Model warmup lifecycle                          │  │
│  └───────────────────────────────────────────────────┘  │
│                                                         │
│  ┌───────────────────────────────────────────────────┐  │
│  │ Model & Dataset Governance                        │  │
│  │   Model versioning, dataset versioning            │  │
│  │   Annotation provenance, evaluation regression    │  │
│  │   Reality asset integration                       │  │
│  └───────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

---

## 2. Runtime boundary (formalized)

### 2.1 What Runtime may consume

Runtime may consume these perception artifacts through the frozen `IEnvironment`
port. Each is an evidence fact, not semantic truth.

| Artifact | Type | Delivered via | Frozen invariant |
|---|---|---|---|
| Perception evidence | `ObservedElement[]` | `Observation.Elements` | Evidence, not semantic truth (I-4) |
| Normalized candidates | `PerceptionCandidate` (adapter-internal) | `IPerceptionSource.AnalyzeAsync` → translated to `ObservedElement` | Adapter-private; does not cross Runtime boundary |
| Element bounds | `ElementBounds` (normalized [0,1]×[0,1]) | `ObservedElement.Bounds` | Spatial evidence, not element identity |
| OCR text | `string` | `ObservedElement.Text` | Raw text ≠ semantic element identity |
| Element type label | `string` (PerceptionType) | `ObservedElement.PerceptionType` | Provider evidence, not Runtime semantic truth |
| Switch state | `bool?` | `ObservedElement.SwitchState` | Qualitative three-state: ON/OFF/UNKNOWN |
| Frame identity | `PerceptionFrame` (opaque Guid) | `ISwitchStateReader.Frame` | Immutable per-capture; stale-frame detection |
| Observation sequence | `Observation` | `IEnvironment.ObserveAsync` | Evidence snapshot, monotonic sequence number |

### 2.2 What Runtime MUST NOT depend on

This is the frozen exclusion list. Any future architecture proposal that
introduces a dependency on these SHALL be rejected at architecture gate.

| Forbidden dependency | Reason |
|---|---|
| Python runtime (`python3`, `python3.11`, any Python version) | Language boundary violation; Runtime is .NET |
| `ultralytics` / YOLO | Model dependency; belongs to Perception Platform |
| PaddleOCR / RapidOCR / ONNX Runtime | OCR dependency; belongs to Perception Platform |
| `.pt` / `.onnx` / `.pth` model files | Model artifact; Runtime must not load ML models |
| PIL / Pillow / OpenCV / any image library beyond SkiaSharp | Image processing boundary; SkiaSharp is screenshot transport, not perception |
| HTTP / UDS / gRPC transport to vision service | Transport dependency; belongs to Adapters layer, not Runtime |
| `label-mapping.json` / label alias tables | Configuration dependency; Adapters may reference, Runtime must not |
| Fusion algorithm (`fusion.py` logic) | Algorithm dependency; Runtime consumes fused output, not algorithm |
| `VisionEvidence` / `VisionCandidate` DTOs | Schema dependency; Adapter-internal deserialization only |
| `PerceptionCandidate` (the adapter-internal record) | Adapter-private type; does not cross `IEnvironment` |

### 2.3 The IEnvironment contract (carried forward, unchanged)

```csharp
// Frozen. No new methods. No new parameters. No new types.
public interface IEnvironment
{
    Task<Observation> ObserveAsync(CancellationToken cancellationToken);
    Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken);
}
```

`Observation` carries `ImmutableArray<ObservedElement>`. That is the complete
perception evidence surface available to Runtime. No new fields, no new methods,
no new types cross this boundary.

### 2.4 Existing adapter-private interfaces (formalized)

These interfaces exist in `UniClaw.Runtime.Adapters` and are NOT Runtime ports.
They are adapter-internal seams for test substitution. They may evolve without
Runtime governance as long as `IEnvironment` is unchanged.

```csharp
// Adapter-private. NOT a Runtime port.
public interface IScreenshotSource
{
    Task<ScreenshotCapture> CaptureAsync(CancellationToken cancellationToken);
}

// Adapter-private. NOT a Runtime port.
public interface IPerceptionSource
{
    Task<ImmutableArray<PerceptionCandidate>> AnalyzeAsync(
        SkiaSharp.SKBitmap screenshot, int width, int height,
        CancellationToken cancellationToken);
}

// Adapter-private. NOT a Runtime port.
public interface IAdbDispatchTarget
{
    Task<ActionResult> ExecuteAsync(AdbOperation operation,
        CancellationToken cancellationToken);
}
```

### 2.5 The ISwitchStateReader boundary (current status)

`ISwitchStateReader` is defined in `UniClaw.Runtime/Capabilities/Perception/Vision/`.
Per the Capability Module Architecture Final Gate, it is:

```text
ISWITCHSTATEREADER = UNPURCHASED_L2_CONTRACT_CANDIDATE
```

It is NOT a graduated Runtime port. Its location under `Runtime/` is an
acknowledged placement issue — it should reside in `Runtime.Adapters/` as an
adapter-internal interface, not in `Runtime/` as if it were a semantic port.

**This gate does NOT authorize ISwitchStateReader.** The contract, frame
lifetime, production composition, and provider boundary must be purchased
separately. The current `ImageSwitchStateProvider` (deterministic geometry
analysis in C#) is acceptable as an adapter-internal mechanism; the
`ISwitchStateReader` interface location is a placement issue to resolve,
not a behavioral defect.

---

## 3. Perception Platform ownership

### 3.1 Platform scope

The Perception Platform is an owned capability domain. It is NOT a black-box
external service. It lives under project source control, is versioned alongside
Runtime, and is subject to the same architecture governance.

| Asset | Owner | Repository location |
|---|---|---|
| Vision Service (Python FastAPI) | Perception Platform | `platforms/perception/vision-service/` |
| YOLO model lifecycle | Perception Platform | `platforms/perception/models/yolo/` |
| OCR backend | Perception Platform | `platforms/perception/models/ocr/` |
| Fusion engine | Perception Platform | `platforms/perception/fusion/` |
| Label mapping configuration | Perception Platform (single source of truth) | `platforms/perception/config/label-mapping.json` |
| Image preprocessing | Perception Platform | `platforms/perception/preprocessing/` |
| Provider Host | Perception Platform | `platforms/perception/host/` |
| Dataset lifecycle | Perception Platform | `platforms/perception/datasets/` |
| Training pipeline | Perception Platform | `platforms/perception/training/` |
| Evaluation regression suite | Perception Platform | `platforms/perception/evaluation/` |
| Reality asset integration | Perception Platform + Harness | `platforms/perception/reality/` (with Harness-owned capture references) |
| C# Adapter (`LocalVisionPerceptionSource`) | Runtime Adapters | `src/UniClaw.Runtime.Adapters/Perception/` |
| C# SwitchStateReader (`ImageSwitchStateProvider`) | Runtime Adapters | `src/UniClaw.Runtime.Adapters/Perception/Vision/` |

### 3.2 Ownership principles

1. **Single source of truth.** Label mapping, model configuration, and
   preprocessing parameters have ONE authoritative file. The Python service
   and C# adapter both reference it — Python at runtime, C# at test/build
   time for schema validation.

2. **Platform owns the pipeline, Runtime owns the boundary.** The Perception
   Platform may change YOLO version, OCR backend, fusion algorithm, or
   preprocessing strategy without Runtime governance — as long as the output
   contract (§4) is satisfied.

3. **Versioned contracts, not coupled releases.** Perception Platform and
   Runtime release independently. Compatibility is negotiated through the
   versioned service contract, not through lockstep version numbers.

4. **Platform is testable in isolation.** The Perception Platform has its
   own test suite (already exists: `tests/test_server.py`,
   `tests/test_fusion.py`, `tests/test_backends_fusion.py`), its own
   evaluation benchmarks (`benchmark_raw.py`), and its own reality assets.
   It does not require Runtime to validate its output.

5. **Reality assets flow one way.** Recorded screenshots, golden observations,
   and annotated ground truth flow from Harness/Reality corpus → Perception
   Platform. Perception output flows Perception Platform → Runtime Adapters →
   Runtime. No reverse flow.

---

## 4. Vision Service contract

### 4.1 Contract versioning

```text
Contract: uniclaw.perception.v1
Schema:   uniclaw.localVisionEvidence.v1
Transport: HTTP/1.1 over Unix Domain Socket
Content-Type: application/octet-stream (request), application/json (response)
```

### 4.2 Input contract

```
POST /v1/analyze
Content-Type: application/octet-stream

Body: JPEG-encoded image bytes (quality ≥ 92)
      Full screenshot, any resolution.

Headers: (none required)
```

Alternative raw path (for zero-decode performance):

```
POST /v1/analyze_raw
Content-Type: application/octet-stream
X-Image-Width: <int>
X-Image-Height: <int>
X-Image-Pixel-Format: 1  (RGBA)

Body: raw RGBA pixel buffer (width × height × 4 bytes)
```

### 4.3 Output contract

```json
{
  "image": {
    "width": 1440,
    "height": 3168
  },
  "yolo": [
    {
      "id": "det_0",
      "label": "switch",
      "confidence": 0.81,
      "bounds": { "x1": 0.805, "y1": 0.395, "x2": 0.913, "y2": 0.425 },
      "boundsPx": [1160, 1251, 1314, 1346],
      "center": { "x": 0.859, "y": 0.410 },
      "centerPx": [1237, 1299]
    }
  ],
  "ocr": [
    {
      "id": "ocr_0",
      "text": "Wi‑Fi",
      "confidence": 0.92,
      "bounds": { "x1": 0.05, "y1": 0.10, "x2": 0.20, "y2": 0.14 },
      "boundsPx": [72, 317, 288, 444],
      "center": { "x": 0.125, "y": 0.120 },
      "centerPx": [180, 381]
    }
  ],
  "candidates": [
    {
      "id": "candidate_1",
      "type": "toggle",
      "text": "",
      "confidence": 0.81,
      "bounds": { "x1": 0.805, "y1": 0.395, "x2": 0.913, "y2": 0.425 },
      "boundsPx": [1160, 1251, 1314, 1346],
      "center": { "x": 0.859, "y": 0.410 },
      "centerPx": [1237, 1299],
      "evidence": {
        "yoloId": "det_0",
        "ocrIds": [],
        "allIds": ["det_0"]
      },
      "riskFlags": ["no_text_evidence"]
    }
  ],
  "summary": {
    "yoloCount": 14,
    "ocrCount": 23,
    "candidateCount": 12,
    "unmatchedOcrCount": 3
  },
  "scrollHints": {
    "totalCandidates": 12,
    "candidatesNearBottom": 2,
    "scrollbarDetected": false
  },
  "metadata": {
    "schema": "uniclaw.localVisionEvidence.v1",
    "width": 1440,
    "height": 3168,
    "pipeline": { "name": "local-vision", "version": "1.0" },
    "models": {
      "yolo": "artifacts/local-vision/models/android_ui_detection_yolov8/best.pt",
      "ocr": "rapidocr"
    },
    "configHash": "sha256:abc123..."
  }
}
```

### 4.4 Contract invariants

**Output MUST contain:**
- Structured evidence only: YOLO detections, OCR tokens, fused candidates,
  scroll hints, metadata.
- All coordinates normalized to [0,1]×[0,1] in the original full-screenshot
  frame (top-left origin). Preprocessing (crop, resize) is transparent to
  the consumer — coordinates are always remapped to original space.
- `bounds` always in normalized space. `boundsPx` always in original
  device pixel space.
- `metadata.configHash` — SHA-256 of the label-mapping.json used to produce
  this output. Enables consumer to detect config drift.
- `metadata.schema` — version identifier for the output schema.

**Output MUST NOT contain:**
- Action decisions ("tap this", "scroll here").
- Semantic goals ("navigate to Wi‑Fi", "enable Bluetooth").
- Capability selection ("this element is a menu_item, therefore navigable").
- State assertions beyond perception ("Wi‑Fi is connected" — that's a Runtime
  semantic inference, not a perception fact).
- Confidence thresholds or decision boundaries (riskFlags are evidence tags,
  not decisions).

### 4.5 Consumer responsibilities

The C# adapter (`LocalVisionPerceptionSource`) is responsible for:
1. Deserializing JSON into `VisionEvidence` DTOs.
2. Mapping `VisionCandidate` → `PerceptionCandidate` (adapter-internal type).
3. Validating `ElementBounds.IsValid` before constructing `PerceptionCandidate`.
4. Translating `PerceptionCandidate` → `ObservedElement` with correct
   `PerceptionType` normalization.
5. Discarding candidates with invalid bounds (fail closed).
6. Returning empty array on HTTP error, deserialization failure, or null
   evidence (fail closed, truthful).

The adapter MUST NOT:
- Interpret confidence values to filter candidates.
- Reclassify element types beyond the label mapping table.
- Infer semantic properties from perception output.
- Cache or reuse candidates across observations.

### 4.6 Contract evolution

| Version | Change | Migration |
|---|---|---|
| v1 (current) | Initial schema | — |
| v1.1 (future) | Add `candidate.stateEvidence` for switch/toggle ON/OFF | Backward-compatible: new optional field |
| v2 (future) | Add `candidate.interactionHints` (clickable, scrollable, long-pressable) | New schema version; v1 adapter ignores v2 fields |
| v2 (future) | Add `pageStructure` (container hierarchy, list boundaries) | New schema version; requires adapter update |

Contract version is negotiated at Provider Host startup (§6). The adapter
declares its maximum supported schema version; the service responds with the
highest mutually supported version.

---

## 5. Model governance

### 5.1 Model inventory

| Model | Purpose | Current version | Source | Update cadence |
|---|---|---|---|---|
| `android_ui_detection_yolov8` | UI element detection (21 Deki-Yolo labels) | `best.pt` (deki-yolo) | External (Deki-Yolo project) + fine-tune | Manual — verified against regression suite |
| RapidOCR | Text recognition (DBNet detection + CRNN recognition) | ONNX Runtime (pip `rapidocr_onnxruntime`) | External (RapidAI) | Locked to pip version; upgrade gated on benchmark regression |
| Fusion engine | YOLO+OCR spatial matching, chevron heuristic, search-box pre-labeling | In-repo (`fusion.py`) | Internal | Per contract version |

### 5.2 Model versioning

Every model is identified by a stable triplet:

```text
MODEL_ID = {name}/{version_hash}
  name:         human-readable model name
  version_hash: SHA-256 of model file (first 12 hex chars)

Example: android_ui_detection_yolov8/a1b2c3d4e5f6
```

The `metadata.models` field in the service output carries the active model
identity. Every perception output is traceable to the exact model that
produced it.

### 5.3 Dataset versioning

```text
DATASET_ID = {name}/{version}
  name:    human-readable dataset name
  version: ISO date of snapshot (YYYY-MM-DD)

Example: settings-screenshots-chinese-rom/2026-08-01
```

Datasets are immutable snapshots. A new annotation pass on the same images
produces a new dataset version. Previous versions remain available for
regression comparison.

### 5.4 Annotation provenance

Every annotated example carries:

```text
ANNOTATION_RECORD
  = dataset_id
  + image_hash (SHA-256 of screenshot)
  + annotator (human | model-v1 | model-v2 | consensus)
  + annotation_date
  + ground_truth fields:
      - element_bounds[] (pixel coords)
      - element_labels[] (Deki-Yolo 21-label vocabulary)
      - element_text[] (ground-truth OCR text)
      - switch_states[] (ON/OFF for toggle regions)
      - page_type (settings_home, settings_sub, app_drawer, ...)
  + review_status (unreviewed | reviewed | challenged | corrected)
```

"Consensus" annotator means: ≥2 human annotators agreed; disagreement flagged
for review.

### 5.5 Evaluation regression

Every model change (new YOLO version, OCR backend upgrade, fusion algorithm
change) SHALL pass the evaluation regression suite before deployment:

| Metric | Threshold | Degradation action |
|---|---|---|
| mAP@0.5 (YOLO detection) | No regression > 2% absolute | BLOCK deployment |
| OCR character accuracy | No regression > 1% absolute | BLOCK deployment |
| Candidate fusion accuracy (type + text + bounds) | No regression > 2% absolute | BLOCK deployment |
| Switch state classification accuracy | No regression > 3% absolute | BLOCK deployment |
| Inference latency (p50) | No regression > 20% relative | WARN; human review |
| Inference latency (p99) | No regression > 50% relative | BLOCK deployment |
| Memory (RSS after warmup) | No regression > 30% relative | WARN; human review |

The regression suite runs against a fixed golden dataset. New datasets may be
added; golden datasets are never removed (only deprecated with documented
reason).

### 5.6 Reality asset integration

The Perception Platform consumes reality assets from the Harness-owned corpus:

```text
Harness Reality Corpus
  ├── golden-screenshots/     (E4: raw recorded emulator/device)
  ├── reality-replays/         (E3: executable replay from recorded reality)
  └── annotated-ground-truth/  (E2: human-annotated or consensus)
      │
      v
Perception Platform
  ├── evaluation/              (regression suite consumes golden + annotated)
  ├── training/                (fine-tuning consumes annotated)
  └── reality/                 (validation consumes reality replays)
```

The Perception Platform reads reality assets; it does not write to the Harness
corpus. Reality asset provenance (E2, E3, E4) is preserved — the platform does
not upgrade evidence maturity.

---

## 6. Provider Host

### 6.1 Host responsibilities

The Provider Host is the lifecycle manager for the Vision Service. It is owned
by the Perception Platform and runs as a separate OS process.

| Responsibility | Mechanism |
|---|---|
| **Startup** | Launch Python process, wait for `/health` warm=true, enforce startup timeout |
| **Health monitoring** | Periodic `GET /health` polling; detect stall, OOM, crash |
| **Shutdown** | SIGTERM → wait → SIGKILL escalation; drain in-flight requests |
| **Crash recovery** | Detect process exit; restart with backoff (1s, 2s, 4s, 8s, max 30s); surface crash count |
| **Version compatibility** | Negotiate schema version at startup; refuse to start if incompatible |
| **Resource limits** | Enforce memory limit (cgroup / ulimit); enforce CPU affinity |
| **Model warmup** | Ensure YOLO model loaded + OCR engine initialized before marking warm=true |

### 6.2 Host startup sequence

```text
1. C# adapter requests Perception Platform startup.
2. Provider Host:
   a. Validate Python version (≥ 3.11).
   b. Validate required packages (ultralytics, rapidocr_onnxruntime, fastapi).
   c. Validate model file exists and hash matches expected.
   d. Validate label-mapping.json exists and parses.
   e. Launch uvicorn with vision service app.
   f. Poll GET /health every 500ms until warm=true or timeout (60s).
   g. If timeout: kill process, report startup failure.
3. Provider Host returns:
   - socket_path: Unix Domain Socket path for HTTP transport
   - schema_version: negotiated schema version
   - model_id: active model identity triplet
   - config_hash: SHA-256 of label-mapping.json
4. C# adapter constructs LocalVisionPerceptionSource with socket_path.
```

### 6.3 Health states

| State | Condition | Consumer behavior |
|---|---|---|
| `COLD` | Process not yet started | Initiate startup |
| `WARMING` | Process running, warm=false | Wait; do not send requests |
| `HEALTHY` | warm=true, recent /health OK | Normal operation |
| `DEGRADED` | warm=true, /health latency > threshold | Continue with warning; prepare fallback |
| `UNHEALTHY` | /health returning error or timeout | Stop sending requests; initiate recovery |
| `CRASHED` | Process exited unexpectedly | Record crash; restart with backoff |
| `SHUTDOWN` | Graceful shutdown initiated | Drain in-flight; reject new |

### 6.4 Crash handling

```text
CRASH_COUNT = 0
MAX_CRASHES_BEFORE_ESCALATION = 5 (per Runtime session)

On crash:
  1. Record crash timestamp, exit code, last stderr lines.
  2. CRASH_COUNT += 1.
  3. If CRASH_COUNT > MAX_CRASHES_BEFORE_ESCALATION:
     - Surface HARNESS_OPERATIONAL_FAILURE episode.
     - Do NOT restart — repeated crashes indicate unrecoverable state.
     - Runtime may continue with PerceptionSource returning empty arrays
       (fail closed, truthful — Observation.Elements will be empty).
  4. Else:
     - Wait backoff(CRASH_COUNT): min(2^CRASH_COUNT, 30) seconds.
     - Restart from step 2 of startup sequence.
```

### 6.5 Version compatibility

At startup, the C# adapter declares `MAX_SCHEMA_VERSION = "v1"`. The Provider
Host checks the vision service's supported versions and negotiates:

```text
Negotiation:
  Adapter declares:  ["v1"]
  Service supports:  ["v1", "v1.1"]
  → Negotiated: "v1"  (highest mutually supported)

  Adapter declares:  ["v2"]
  Service supports:  ["v1", "v1.1"]
  → Negotiated: FAIL  (no overlap → refuse to start)
```

If the C# adapter and vision service cannot agree on a schema version, the
Provider Host refuses to start. This is a deployment configuration error,
not a runtime fallback.

---

## 7. Migration strategy

### 7.1 Current state

```
uni-claw/tools/local_vision/
  server.py         — FastAPI app, pipeline orchestration
  backends.py       — YOLO inference, OCR backends, image preprocessing
  fusion.py         — YOLO+OCR spatial fusion, heuristics
  schema.py         — Detection, OcrToken, Box data classes
  analyze.py        — CLI entry point
  label-mapping.json — label mapping + spatial config
  requirements.txt  — Python dependencies
  benchmark_raw.py  — performance benchmarks
  tests/            — test_server.py, test_fusion.py, test_backends_fusion.py
```

```
uni-agent/src/UniClaw.Runtime.Adapters/Perception/
  LocalVisionPerceptionSource.cs  — C# adapter (HTTP/UDS client)
  Vision/
    ImageSwitchStateProvider.cs   — C# deterministic switch reader
```

### 7.2 Target state

```
platforms/perception/
  vision-service/
    server.py            — migrated from tools/local_vision/server.py
    backends.py          — migrated from tools/local_vision/backends.py
    fusion.py            — migrated from tools/local_vision/fusion.py
    schema.py            — migrated from tools/local_vision/schema.py
    requirements.txt     — migrated, version-pinned
  host/
    provider_host.py     — NEW: lifecycle manager (startup, health, shutdown, crash)
    version_negotiation.py — NEW: schema version compatibility
  models/
    yolo/
      android_ui_detection_yolov8/
        best.pt          — migrated from artifacts/
        model_card.md    — NEW: provenance, training data, known limitations
    ocr/
      rapidocr_config.json — NEW: OCR backend configuration
  fusion/
    label-mapping.json   — migrated, single source of truth
  config/
    preprocessing.json   — NEW: extracted from label-mapping.json spatial section
  datasets/
    README.md            — dataset catalog
    settings-chinese-rom/
      manifest.json      — image list + annotations
  evaluation/
    regression_suite.py  — migrated + extended from benchmark_raw.py
    golden_dataset.json  — NEW: pinned evaluation dataset reference
  training/
    README.md            — fine-tuning instructions
  reality/
    README.md            — reality asset integration documentation
  tests/
    test_server.py       — migrated
    test_fusion.py       — migrated
    test_backends_fusion.py — migrated
    test_provider_host.py   — NEW
    test_version_negotiation.py — NEW

uni-agent/src/UniClaw.Runtime.Adapters/Perception/
  LocalVisionPerceptionSource.cs  — UPDATED: use Provider Host, version negotiation
  PerceptionProviderHost.cs       — NEW: C# side of Provider Host protocol
  Vision/
    ImageSwitchStateProvider.cs   — unchanged (C#-side deterministic reader)
```

### 7.3 Migration phases

#### Phase 0 — Freeze (current → this gate)

- No files move. No code changes.
- This gate is published as the architecture contract.
- Existing `tools/local_vision/` continues to operate unchanged.
- All new perception work references this gate for boundary decisions.

#### Phase 1 — Extract contract (no file moves)

- Publish the Vision Service contract as a standalone document
  (`platforms/perception/CONTRACT.md`).
- Add `metadata.schema` version field to server.py output (already present
  as `"uniclaw.localVisionEvidence.v1"`).
- Add `GET /version` endpoint returning supported schema versions.
- Add `configHash` to output (already present in `_metadata()`).
- No file moves. No Runtime changes.

#### Phase 2 — Provider Host (new code only)

- Implement `provider_host.py` with startup, health polling, shutdown,
  crash recovery.
- Implement `version_negotiation.py`.
- Add `PerceptionProviderHost.cs` in Runtime.Adapters.
- Test host lifecycle in isolation (no Runtime).
- `server.py` unchanged. Existing `tools/local_vision/` still works.

#### Phase 3 — Migrate files (move, don't rewrite)

- Move `tools/local_vision/` → `platforms/perception/vision-service/`.
- Update import paths. Update test paths.
- Update CI/CD to point to new location.
- Update `LocalVisionPerceptionSource` to use Provider Host instead of
  direct socket path.
- `tools/local_vision/` becomes a symlink or deprecation notice for
  one release cycle, then removed.

#### Phase 4 — Governance activation

- Pin model versions in `model_card.md`.
- Freeze golden evaluation dataset.
- Activate regression suite as CI gate.
- Dataset versioning and annotation provenance tooling.
- Training pipeline documentation.

### 7.4 Dependency isolation

The Perception Platform has exactly one runtime dependency from the C#
side: the `LocalVisionPerceptionSource` adapter. This dependency is:

- **Compile-time:** `UniClaw.Runtime.Adapters` references no Python code.
  It references only the Vision Service contract (JSON schema).
- **Runtime:** Unix Domain Socket to a separately launched Python process.
  The C# process has zero Python libraries loaded.
- **Test-time:** Integration tests may require the Python service to be
  running. Unit tests use a mock `IPerceptionSource`.

The Perception Platform Python environment is isolated:
- Dedicated `.venv` (already exists: `.venv-local-vision`).
- Pinned `requirements.txt` with exact versions.
- Model files in `artifacts/` directory, not in Python package path.
- No dependency on Runtime .NET assemblies or build artifacts.

### 7.5 Compatibility during migration

Throughout all phases:

- `IEnvironment` remains unchanged.
- `Observation`, `ObservedElement`, `ElementBounds` remain unchanged.
- `IPerceptionSource` (adapter-private) may evolve; `IEnvironment` must not.
- The JSON output schema is backward-compatible within a major version.
- Existing replay assets, golden runs, and Scenario tests continue to pass.
- The `tools/local_vision/` directory remains functional until Phase 3
  migration is complete and verified.

---

## 8. Constraints (reaffirmed)

### 8.1 What this gate does NOT authorize

- **NO** Provider framework in Runtime. `IEnvironment` is the ONLY Runtime
  port. `IPerceptionSource` is adapter-private, not a framework extension
  point.
- **NO** Capability registry. Perception is not a capability to be discovered
  or selected at runtime — it is infrastructure.
- **NO** Brain, Planner, or StateClassifier in Runtime. These remain
  rejected concepts per the Capability Module Architecture Final Gate.
- **NO** Runtime semantic changes. Agent, Container, Traversal, and
  Environment contracts are frozen.
- **NO** new `ObservedElement` fields without a separate architecture gate.
- **NO** perception-internal types crossing the `IEnvironment` boundary.
- **NO** implementation of Phase 1–4 without separate task authorization.

### 8.2 Architecture invariants preserved

1. **Runtime spine purity.** Runtime depends on `IEnvironment` only. Zero
   perception implementation details leak through.
2. **Evidence, not truth.** Every perception artifact crossing `IEnvironment`
   is an evidence fact, not semantic truth. Runtime adjudicates.
3. **One-way data flow.** Screenshots → Perception Platform → Adapter →
   Runtime. No reverse flow. No Runtime callbacks into perception.
4. **Platform independence.** Perception Platform can be developed, tested,
   benchmarked, and deployed without Runtime.
5. **Adapter isolation.** Adapter changes (new `IPerceptionSource`
   implementation, Provider Host integration) do not require Runtime
   governance.
6. **Fail closed.** Perception failure → empty candidates → empty
   Observation.Elements → Runtime sees UNKNOWN world. Never fabricated
   evidence.

---

## 9. Relationship to existing gates

| Gate | Relationship |
|---|---|
| **Capability Module Architecture Final Gate** | Perception is NOT a capability module. It is infrastructure below `IEnvironment`. `ISwitchStateReader` remains unpurchased; its placement under `Runtime/` is acknowledged as a placement issue. |
| **Runtime Observability Trace Graduation** | Perception is outside Runtime observability scope. The `environment.observe` span covers `ObserveAsync` latency; internal perception pipeline timing is captured by the Provider Host, not by Runtime trace. |
| **State Evidence Bridge Challenge** | The StateClassifier fast/slow path design is compatible with this gate. StateClassifier lives in the Perception Platform (Python or C# adapter), not in Runtime. The `ObservedElement.SwitchState` field is the contract point. |
| **Failure Episode Reality Model** | Perception failure (empty candidates, service crash, timeout) is a HARNESS_OPERATIONAL_FAILURE or OBSERVATION_UNAVAILABLE episode boundary. It is never a Runtime failure. |

---

## 10. Gate status

```text
PERCEPTION_PLATFORM_ARCHITECTURE_GATE
  = GATE_DEFINED

ARCHITECTURE_PURCHASE
  = NOT_PURCHASED

IMPLEMENTATION_AUTHORITY
  = NOT_GRANTED

RUNTIME_DELTA
  = NONE

OWNERSHIP_DELTA
  = NONE (formalized, not changed)

CONTRACT_DELTA
  = NONE (formalized existing, not changed)
```

This gate defines the long-term architecture boundary. It does not authorize
any file moves, code changes, new endpoints, new types, or new infrastructure.
Each migration phase requires separate authorization with explicit task
definition, acceptance criteria, and regression gates.

### 10.1 Required future gates

Before any implementation:

1. **Phase 1 authorization:** Contract extraction + `/version` endpoint.
   Requires: this gate accepted, contract document reviewed.
2. **Phase 2 authorization:** Provider Host implementation.
   Requires: Phase 1 complete, host lifecycle tests passing in isolation.
3. **Phase 3 authorization:** File migration + adapter update.
   Requires: Phase 2 complete, full Runtime regression passing with migrated
   perception platform.
4. **Phase 4 authorization:** Governance activation.
   Requires: Phase 3 complete, golden evaluation dataset frozen, regression
   suite integrated into CI.
5. **ISwitchStateReader purchase gate:** Separate gate to purchase the
   contract, frame lifetime, and production composition. Not blocked by
   this gate; may proceed independently.

---

## 11. Explicit non-actions

- No file moves from `tools/local_vision/` to `platforms/perception/`.
- No new Python code (Provider Host, version negotiation).
- No new C# code (PerceptionProviderHost, adapter changes).
- No new endpoints on the vision service.
- No Runtime type changes, interface changes, or field additions.
- No model file changes, dataset creation, or evaluation suite changes.
- No CI/CD changes.
- No Provider framework, Capability registry, Brain, Planner, or StateClassifier.
- No ISwitchStateReader purchase.

`PERCEPTION_PLATFORM_ARCHITECTURE_GATE_RESULT`

STOP.
