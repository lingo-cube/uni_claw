# Perception Platform Contract Extraction Result

> Date: 2026-08-12
> Role: Project Leader / Architecture Gate Reviewer
> Lane: `ARCHITECTURE_DISCOVERY`
> Input: `docs/decisions/perception-platform-architecture-gate.md`
> Result: `PERCEPTION_PLATFORM_CONTRACT_EXTRACTION_RESULT`
> Gate decision: **PURCHASE** (Phase 1 only)
> Implementation authority: **GRANTED — Phase 1 only**

---

## 0. Review decision

```text
PERCEPTION_PLATFORM_ARCHITECTURE_GATE_REVIEW
  = PURCHASE

PHASE_1_AUTHORIZED
  = YES

PHASE_2_3_4_AUTHORIZED
  = NO

RUNTIME_DELTA
  = NONE

OWNERSHIP_DELTA
  = NONE
```

The architecture gate is **purchased**. The formalization of perception as an
owned platform with versioned contracts, model governance, Provider Host
lifecycle, and four-phase migration is accepted as the long-term architecture
direction.

**Only Phase 1 is authorized for implementation.** Phases 2–4 require separate
task authorization after Phase 1 is complete and verified.

---

## 1. Why PURCHASE

### 1.1 Architecture integrity verified

The gate formalizes what already exists. It does not invent new boundaries:

| Claim in gate | Ground truth in codebase |
|---|---|
| Runtime depends only on `IEnvironment` | [IEnvironment.cs](src/UniClaw.Runtime/Environment/IEnvironment.cs) — two methods, frozen |
| Perception flows through `PhysicalEnvironment` adapter | [PhysicalEnvironment.cs](src/UniClaw.Runtime.Adapters/PhysicalEnvironment.cs) — composes `IScreenshotSource` + `IPerceptionSource` + `IAdbDispatchTarget` |
| Vision service outputs structured JSON | [server.py](uni-claw/tools/local_vision/server.py) — `_run_pipeline()` returns `evidence` dict with `candidates`, `yolo`, `ocr`, `metadata` |
| `metadata.schema` identifies output version | [server.py:428](uni-claw/tools/local_vision/server.py) — `"uniclaw.localVisionEvidence.v1"` in `_metadata()` |
| `configHash` enables config drift detection | [server.py:77](uni-claw/tools/local_vision/server.py) — `_CONFIG_HASH = hashlib.sha256(content).hexdigest()` in `_load_spatial()` |
| Adapter-private interfaces are not Runtime ports | [PhysicalEnvironment.cs:182-209](src/UniClaw.Runtime.Adapters/PhysicalEnvironment.cs) — `IScreenshotSource`, `IPerceptionSource`, `IAdbDispatchTarget` all in `Runtime.Adapters` namespace |
| Adapter fails closed on HTTP error | [LocalVisionPerceptionSource.cs:72-73](src/UniClaw.Runtime.Adapters/Perception/LocalVisionPerceptionSource.cs) — `if (!response.IsSuccessStatusCode) return [];` |

No fabrication. Every architectural claim maps to existing code.

### 1.2 Exclusion list is correct and complete

The 12-item forbidden dependency list (§2.2) is verified against the current
codebase:

| Forbidden dependency | Present in Runtime? | Present in Adapters? | Verdict |
|---|---|---|---|
| Python runtime | No | No (separate process) | Clean |
| YOLO / ultralytics | No | No (only in Python service) | Clean |
| OCR libraries | No | No | Clean |
| Model files | No | No (only referenced by Python service) | Clean |
| PIL / OpenCV | No | No (SkiaSharp is screenshot transport only) | Clean |
| HTTP / UDS transport | No | Yes (in `LocalVisionPerceptionSource`) — correct placement in Adapters | Correct |
| label-mapping.json | No | No (C# adapter uses only `VisionCandidate` DTOs, not raw config) | Clean |
| Fusion algorithm | No | No | Clean |
| VisionEvidence DTOs | No | Yes (file-scoped in `LocalVisionPerceptionSource.cs`) — correct placement in Adapters | Correct |

Items marked "in Adapters" are correctly placed — they belong at the adapter
layer, not in Runtime.

### 1.3 Phase 1 is safe to authorize

Phase 1 (contract extraction) satisfies all stated constraints:

| Constraint | Phase 1 behavior |
|---|---|
| No Runtime changes | CONTRACT.md is a standalone document. No .cs file touched. |
| No Runtime dependency on Python | Unchanged. |
| No model migration yet | Unchanged. `tools/local_vision/` stays in place. |
| No Provider Host implementation yet | Deferred to Phase 2. |
| No YOLO training changes yet | Unchanged. |

Phase 1 is additive: one document + one endpoint. Existing behavior is
unchanged. Full Runtime regression (819/819) and architecture guards (16/16)
must continue to pass.

### 1.4 Risks are acceptable

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Contract document drifts from implementation | Medium | Low — document reflects current state; drift detected at Phase 2 review | `/version` endpoint returns actual supported versions; contract doc references it |
| `GET /version` endpoint implementation breaks existing server | Low — additive endpoint, no existing behavior changed | Medium — would break perception if server doesn't start | Test with existing `tests/test_server.py`; verify `/health` still works |
| Phase 1 creates false sense of completion | Low | Low — Phase 1 is explicitly scoped as "contract extraction only" | This document explicitly gates Phases 2–4 |

---

## 2. Why NOT Phases 2–4

### 2.1 Phase 2 (Provider Host) is not authorized

Phase 2 requires:
- New Python code (`provider_host.py`, `version_negotiation.py`)
- New C# code (`PerceptionProviderHost.cs`)
- Host lifecycle testing in isolation
- Crash recovery logic with backoff/retry semantics

These are implementation tasks with behavioral surface area. They require:
- A separate task definition with acceptance criteria
- Architecture review of the crash recovery contract
- Integration testing with the vision service

Phase 2 is correctly designed in the gate but not yet authorized.

### 2.2 Phase 3 (file migration) is not authorized

Phase 3 requires:
- Moving `tools/local_vision/` → `platforms/perception/vision-service/`
- Updating import paths, test paths, CI/CD
- Updating `LocalVisionPerceptionSource` to use Provider Host

File moves across repository boundaries (`uni-claw/` → `uni-agent/` or a shared
`platforms/` location) require repository-level decision-making that is outside
this architecture gate's scope. The target location `platforms/perception/` may
need to be in `uni-claw/`, `uni-agent/`, or a new shared repository — this
decision is not made here.

### 2.3 Phase 4 (governance activation) is not authorized

Phase 4 requires:
- Model version pinning in `model_card.md`
- Golden evaluation dataset freeze
- Regression suite as CI gate
- Dataset versioning and annotation provenance tooling

These require operational commitment (CI resources, dataset storage, annotation
workflow) that is not yet purchased. Phase 4 is gated on Phases 1–3 completion
and a separate governance purchase.

---

## 3. Phase 1 authorization

### 3.1 Authorized deliverables

**D-1: Vision Service Contract document**

Create `platforms/perception/CONTRACT.md` containing:

1. **Contract identifier:** `uniclaw.perception.v1`
2. **Schema identifier:** `uniclaw.localVisionEvidence.v1`
3. **Transport specification:**
   - HTTP/1.1 over Unix Domain Socket
   - Request: `POST /v1/analyze`, `Content-Type: application/octet-stream`, JPEG body
   - Request: `POST /v1/analyze_raw`, headers + raw RGBA body
   - Response: `application/json`, structured evidence schema
4. **Input contract** — exact byte format, headers, constraints
5. **Output contract** — full JSON schema with field descriptions, types,
   required/optional markers, coordinate space specification
6. **Contract invariants** — MUST contain / MUST NOT contain lists from gate §4.4
7. **Consumer responsibilities** — adapter obligations from gate §4.5
8. **Contract evolution policy** — backward-compatibility rules from gate §4.6
9. **Version history** — v1 initial release, dated

The contract document MUST be extractable from the current `server.py` output.
It describes what IS, not what SHOULD BE.

**D-2: Version discovery endpoint**

Add to `server.py`:

```python
@app.get("/version")
async def version():
    """Return supported schema versions for Provider Host negotiation."""
    return {
        "supportedSchemas": ["uniclaw.localVisionEvidence.v1"],
        "serviceVersion": "1.0",
        "modelId": _model_id(),      # SHA-256 of model file, first 12 chars
        "configHash": _CONFIG_HASH,
    }
```

This endpoint is additive only. It does not change any existing behavior:
- `POST /v1/analyze` unchanged
- `POST /v1/analyze_raw` unchanged
- `GET /health` unchanged
- Pipeline (`_run_pipeline`) unchanged
- Preprocessing (`_preprocess`) unchanged
- Coordinate remapping (`_remap_coords`) unchanged

**D-3: Model ID helper**

Add to `server.py` (or `backends.py`):

```python
def _model_id() -> str:
    """Stable model identity: name/hash12."""
    path = Path(_MODEL_PATH)
    if not path.exists():
        return "unknown/missing"
    sha = hashlib.sha256(path.read_bytes()).hexdigest()[:12]
    return f"{path.stem}/{sha}"
```

### 3.2 Acceptance criteria

| # | Criterion | Verification method |
|---|---|---|
| AC-1 | CONTRACT.md accurately describes current server.py output | Manual review: compare CONTRACT.md schema against actual JSON output from a real `/v1/analyze` call on a known screenshot |
| AC-2 | `GET /version` returns 200 with correct fields | Automated: `curl -s --unix-socket /tmp/uniclaw-vision.sock http://localhost/version` returns JSON with `supportedSchemas`, `serviceVersion`, `modelId`, `configHash` |
| AC-3 | `GET /health` still returns `{"status":"ok","warm":true}` | Automated: existing health check behavior unchanged |
| AC-4 | `POST /v1/analyze` still returns valid candidates for a known screenshot | Automated: existing `tests/test_server.py` passes |
| AC-5 | `POST /v1/analyze_raw` still returns valid candidates | Automated: existing raw endpoint tests pass |
| AC-6 | Runtime regression suite passes (819/819) | Automated: `dotnet test` on Runtime solution — no perception changes, must be green |
| AC-7 | Architecture Guards pass (16/16) | Automated: architecture guard tests — no Runtime boundary change |
| AC-8 | `modelId` is stable across server restarts with same model file | Manual: restart server, `/version` returns same `modelId` |
| AC-9 | `configHash` changes when `label-mapping.json` changes | Manual: modify label-mapping.json, restart, `/version` returns different `configHash` |
| AC-10 | `modelId` changes when model file changes | Manual: replace model file, restart, `/version` returns different `modelId` |

### 3.3 File changes authorized

| File | Action | Owner |
|---|---|---|
| `platforms/perception/CONTRACT.md` | CREATE | Perception Platform |
| `uni-claw/tools/local_vision/server.py` | EDIT — add `GET /version` endpoint + `_model_id()` helper | Perception Platform |
| `uni-claw/tools/local_vision/tests/test_server.py` | EDIT — add version endpoint test | Perception Platform |

**NO other files are authorized for change.**

### 3.4 Explicitly NOT authorized

- No file moves from `tools/local_vision/`.
- No changes to `backends.py`, `fusion.py`, `schema.py`, `analyze.py`.
- No changes to `label-mapping.json` or `requirements.txt`.
- No changes to `LocalVisionPerceptionSource.cs` or any C# file.
- No changes to `PhysicalEnvironment.cs`.
- No changes to `IEnvironment`, `Observation`, `ObservedElement`, or any Runtime type.
- No new Python dependencies.
- No Provider Host code (`provider_host.py`, `version_negotiation.py`).
- No C# `PerceptionProviderHost.cs`.
- No CI/CD changes.
- No model file changes.

---

## 4. Phase 1 task breakdown

```text
PHASE_1_TASKS:

T1. [CREATE] platforms/perception/CONTRACT.md
    - Document the Vision Service contract from server.py output schema.
    - Include: contract ID, schema ID, transport, input/output specs,
      invariants, consumer responsibilities, evolution policy.
    - Accuracy: compare against actual JSON output from a real analyze call.

T2. [EDIT] uni-claw/tools/local_vision/server.py
    - Add _model_id() helper function.
    - Add GET /version endpoint.
    - No other changes to server.py.

T3. [EDIT] uni-claw/tools/local_vision/tests/test_server.py
    - Add test for GET /version endpoint:
      - Returns 200.
      - supportedSchemas contains "uniclaw.localVisionEvidence.v1".
      - modelId matches expected format (name/hash12).
      - configHash is non-empty string.

T4. [VERIFY] Runtime regression
    - Run full Runtime test suite: must be 819/819 PASS.
    - Run Architecture Guards: must be 16/16 PASS.

T5. [VERIFY] Vision service tests
    - Run tools/local_vision tests: all existing tests pass.
    - New version endpoint test passes.

T6. [VERIFY] Manual contract accuracy
    - Launch vision service with a known screenshot.
    - Call POST /v1/analyze, capture JSON output.
    - Compare every field in CONTRACT.md against actual JSON.
    - Fix any discrepancies in CONTRACT.md (not in server.py).
```

---

## 5. Authorization status

```text
PERCEPTION_PLATFORM_ARCHITECTURE_GATE
  = PURCHASED

PHASE_1_CONTRACT_EXTRACTION
  = AUTHORIZED

IMPLEMENTATION_SCOPE
  = T1–T6 ONLY

PHASE_2_PROVIDER_HOST
  = NOT_AUTHORIZED

PHASE_3_FILE_MIGRATION
  = NOT_AUTHORIZED

PHASE_4_GOVERNANCE_ACTIVATION
  = NOT_AUTHORIZED

ISWITCHSTATEREADER_PURCHASE
  = NOT_AUTHORIZED (separate gate)

RUNTIME_DELTA
  = NONE

OWNERSHIP_DELTA
  = NONE

AUTHORITY_DELTA
  = NONE
```

Phase 1 is authorized for immediate execution. The contract extraction is the
minimum viable architecture formalization — write down what already exists,
add one discovery endpoint, verify nothing broke.

---

## 6. Explicit non-actions

- No Provider Host implementation.
- No file migration from `tools/local_vision/`.
- No Runtime changes of any kind.
- No `IPerceptionSource` or adapter changes.
- No model governance activation.
- No dataset creation or annotation workflow.
- No training pipeline changes.
- No ISwitchStateReader purchase.
- No Phase 2, 3, or 4 implementation.

`PERCEPTION_PLATFORM_CONTRACT_EXTRACTION_RESULT`

STOP.
