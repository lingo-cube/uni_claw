## ADDED Requirements

### Requirement: Python server accepts raw RGBA screenshots via /v1/analyze_raw

The Python server SHALL expose `POST /v1/analyze_raw` accepting raw RGBA bytes with `Content-Type: application/octet-stream` and mandatory headers `X-Image-Width` (int) and `X-Image-Height` (int). It SHALL support optional header `X-Image-Pixel-Format` defaulting to `1` (RGBA_8888). If `pixel_format != 1`, it SHALL return HTTP 400 with detail message.

The body SHALL be exactly `width * height * 4` bytes. If `len(body) != expected`, it SHALL return HTTP 400 with detail containing expected and actual sizes.

The pipeline for `/v1/analyze_raw` SHALL be: `Image.frombytes("RGBA", (w, h), body)` → `_preprocess(image)` (crop + resize) → `image.convert("RGB")` → YOLO → OCR → fusion → evidence JSON.

The `/v1/analyze_raw` endpoint SHALL reuse the same `_run_pipeline(image, width, height)` function shared with `/v1/analyze`. The response SHALL include the same `Server-Timing` header format.

#### Scenario: Analyze raw returns evidence for valid RGBA buffer

- **WHEN** `POST /v1/analyze_raw` is called with valid RGBA bytes and correct dimension headers
- **THEN** response is 200, body contains `candidates` array, `Server-Timing` header is present

#### Scenario: Body size mismatch returns 400

- **WHEN** `POST /v1/analyze_raw` body length does not equal `width * height * 4`
- **THEN** response is 400 with detail containing "Body size mismatch" and expected/actual sizes

#### Scenario: Unsupported pixel format returns 400

- **WHEN** `POST /v1/analyze_raw` has `X-Image-Pixel-Format: 2`
- **THEN** response is 400 with detail "Unsupported pixel format: 2"

### Requirement: Image preprocessing in Python (crop + resize)

The server SHALL implement `_preprocess(image: Image.Image) -> Image.Image` that applies:
1. Top crop: `int(height * cropTopRatio)` pixels from the top
2. Bottom crop: `int(height * cropBottomRatio)` pixels from the bottom
3. Resize: if `image.width > maxWidth`, scale down proportionally to `maxWidth` using `Image.LANCZOS`

Parameters SHALL be resolved in order: `UNICLAW_IMAGE_MAX_WIDTH` / `UNICLAW_IMAGE_CROP_TOP` / `UNICLAW_IMAGE_CROP_BOTTOM` environment variables → `label-mapping.json` `spatial.preprocessing` fields → defaults (maxWidth=720, cropTop=0.0625, cropBottom=0.0625).

#### Scenario: Crop removes status bar and nav bar

- **WHEN** a 1080×2400 image is preprocessed with default crop ratios (0.0625 each)
- **THEN** the output height is approximately `2400 * (1 - 0.125) = 2100` pixels

#### Scenario: Resize scales down oversized images

- **WHEN** a preprocessed image has width > 720
- **THEN** the output width is exactly 720 and height is proportionally scaled

#### Scenario: Image within maxWidth is not resized

- **WHEN** a preprocessed image has width ≤ 720
- **THEN** the output dimensions are unchanged from input (after crop only)

### Requirement: Shared pipeline function

The server SHALL extract a `_run_pipeline(image: Image.Image, width: int, height: int) -> dict` function containing the YOLO detection → OCR → fusion pipeline, called by both `/v1/analyze` and `/v1/analyze_raw`. The existing `/v1/analyze` endpoint behavior SHALL remain unchanged after this refactoring.

#### Scenario: /v1/analyze unchanged after refactor

- **WHEN** `POST /v1/analyze` is called with a valid JPEG screenshot after `_run_pipeline` extraction
- **THEN** the response is byte-identical to the pre-refactor response (same evidence JSON, same Server-Timing)
