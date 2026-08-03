# roi-ocr-pipeline Specification

## Purpose
Python-side additions to `backends.py` and `fusion.py` enabling zero-disk-I/O YOLO detection + ROI-crop + multi-threaded OCR pipeline. Replaces the current full-image OCR approach with targeted per-detection OCR for 10-20x performance improvement.

## ADDED Requirements

### Requirement: run_yolo_on_image performs memory-based inference

`backends.py` SHALL expose `run_yolo_on_image(image: Image.Image, *, model_path: str, image_size: int, confidence: float, device: str) -> list[Detection]`. It SHALL accept a PIL `Image` object and run Ultralytics YOLO inference in memory (zero disk I/O). The model SHALL be cached at module level via `_get_yolo_model(model_path)`.

Existing `run_yolo` (CLI file-path version) SHALL be preserved and SHALL share the model cache with `run_yolo_on_image`.

#### Scenario: YOLO inference from PIL Image

- **WHEN** `run_yolo_on_image` is called with a valid PIL Image
- **THEN** it returns a list of `Detection` objects with `id`, `label`, `confidence`, `box`, and no temporary files are created

#### Scenario: Model cached across calls

- **WHEN** `run_yolo_on_image` is called twice with the same `model_path`
- **THEN** the Ultralytics model is loaded only once (second call uses cached instance)

#### Scenario: CLI run_yolo preserved

- **WHEN** the existing `run_yolo` function is called with a file path
- **THEN** it works as before and shares the model cache

### Requirement: run_ocr_on_crops performs per-detection OCR

`backends.py` SHALL expose `run_ocr_on_crops(image: Image.Image, detections: list[Detection], *, language: str = "ch", padding: int = 4, max_workers: int | None = None) -> list[list[OcrToken]]`. It SHALL:

1. Crop each detection bounding box from the source image (with padding)
2. Run OCR on each crop using `ThreadPoolExecutor` with per-thread PaddleOCR instances via `threading.local()`
3. Offset crop-local OCR coordinates back to the original image coordinate system
4. Return a list of token lists aligned 1:1 with the input detections

Worker count SHALL default to `UNICLAW_OCR_PARALLEL` env var (default 2), clamped to 1-8.

#### Scenario: Aligned output with detections

- **WHEN** `run_ocr_on_crops` is called with 3 detections
- **THEN** it returns a list of exactly 3 token lists, where index i corresponds to detection i

#### Scenario: Per-thread PaddleOCR isolation

- **WHEN** `run_ocr_on_crops` runs with 2 workers
- **THEN** each worker thread has its own PaddleOCR instance via `threading.local()`, and no C++ thread-safety crashes occur

#### Scenario: Token coordinates offset to original image

- **WHEN** a crop at position (50, 100) yields an OCR token at crop-coordinates (10, 5)
- **THEN** the returned token has coordinates (60, 105) in the original image

#### Scenario: Empty detections returns empty list

- **WHEN** `run_ocr_on_crops` is called with an empty detections list
- **THEN** it returns an empty list without error

#### Scenario: Null crop skipped

- **WHEN** a detection bounding box is entirely outside the image after padding adjustment
- **THEN** the corresponding result slot contains an empty token list `[]`

### Requirement: OCR uses ndarray for zero-disk

`_run_ocr_on_pil` SHALL convert the PIL crop to a numpy array (`np.asarray(crop)[:, :, ::-1]` — RGB to BGR conversion matching `cv2.imread` semantics) and pass it directly to `_call_paddle_ocr`. No temporary files SHALL be created for normal operation. A file-based fallback SHALL exist only for unknown PaddleOCR versions that reject ndarray input.

#### Scenario: ndarray path used in normal operation

- **WHEN** PaddleOCR version supports ndarray input
- **THEN** no temporary files are created during OCR inference

#### Scenario: Fallback for incompatible PaddleOCR

- **WHEN** `_call_paddle_ocr(ocr, ndarray)` raises TypeError
- **THEN** a temporary PNG file is created, used, and cleaned up, and OCR still returns valid results

### Requirement: _call_paddle_ocr accepts Path and ndarray

`_call_paddle_ocr(ocr, source: Path | np.ndarray)` SHALL accept both `Path` objects (normalized to `str` before passing to paddleocr) and `np.ndarray` objects (passed directly). The internal call chain SHALL use `ocr.ocr(source)` with fallback to `ocr.predict(source)`.

#### Scenario: Path normalized to str

- **WHEN** `_call_paddle_ocr` receives a `Path` object
- **THEN** it converts to `str` before calling `ocr.ocr()`

#### Scenario: ndarray passed directly

- **WHEN** `_call_paddle_ocr` receives a numpy ndarray
- **THEN** it passes the ndarray directly to `ocr.ocr()` without conversion

### Requirement: fuse_evidence_from_crops performs direct association

`fusion.py` SHALL expose `fuse_evidence_from_crops(detections: list[Detection], crops_ocr: list[list[OcrToken]], *, image_width: int, image_height: int, promote_unmatched_ocr: bool = False) -> dict`. It SHALL directly associate each detection with its corresponding OCR tokens via `zip(detections, crops_ocr)` — no spatial matching required. `fuse_evidence` (existing full-image version) SHALL be preserved for CLI mode.

`promote_unmatched_ocr` SHALL default to `False` and SHALL NOT promote OCR-only tokens to candidates when false. The `_apply_chevron_heuristic` SHALL be preserved (same-row text_block → menu_item reclassification).

#### Scenario: Direct association no spatial matching

- **WHEN** `fuse_evidence_from_crops` is called with 5 detections and 5 token lists
- **THEN** the output has exactly 5 candidates, each associated with its tokens without IoU or distance computation

#### Scenario: promote_unmatched_ocr=False excludes OCR-only

- **WHEN** `promote_unmatched_ocr` is False and OCR-only tokens exist
- **THEN** those tokens are NOT promoted to `text_block` candidates

#### Scenario: Chevron heuristic preserved

- **WHEN** a text_block detection is in the same row as menu_item detections
- **THEN** it is reclassified as `menu_item` with `evidence.typeInferred: "row_alignment"`

#### Scenario: Existing fuse_evidence preserved

- **WHEN** the existing `fuse_evidence` function is called with full-image OCR results
- **THEN** it works identically to before (CLI mode unaffected)

### Requirement: OCR warmup via long-lived ThreadPoolExecutor

A module-level `ThreadPoolExecutor` SHALL be created once and warmed during lifespan startup. Warmup SHALL submit dummy tasks so each worker thread initializes its `threading.local` PaddleOCR instance. Requests SHALL reuse this executor (no per-request pool creation).

#### Scenario: Executor reused across requests

- **WHEN** multiple `POST /v1/analyze` requests arrive
- **THEN** the same `ThreadPoolExecutor` instance is used for all requests (not recreated each time)

#### Scenario: Warmup initializes thread-local instances

- **WHEN** server startup completes and warmup has run
- **THEN** each worker thread has an initialized PaddleOCR instance, and the first real request does not pay instance creation cost

### Requirement: evidence JSON schema compatibility

The output of `fuse_evidence_from_crops` SHALL conform to `uniclaw.localVisionEvidence.v1` schema: top-level `image`, `yolo[]`, `ocr[]`, `candidates[]`, `summary`, `metadata`, and `scrollHints`. Each candidate SHALL have `id`, `type`, `text`, `confidence`, `confidenceDetail`, `bounds`, `boundsPx`, `center`, `centerPx`, `evidence`, and `riskFlags`.

#### Scenario: Evidence has all required top-level fields

- **WHEN** `fuse_evidence_from_crops` output is inspected
- **THEN** it contains `image`, `yolo`, `ocr`, `candidates`, `summary`, `metadata`, and `scrollHints`

#### Scenario: Candidate has confidence detail

- **WHEN** a candidate is produced with both YOLO detection and OCR text
- **THEN** `confidenceDetail.yolo` is the YOLO confidence and `confidenceDetail.ocr` is the OCR confidence
