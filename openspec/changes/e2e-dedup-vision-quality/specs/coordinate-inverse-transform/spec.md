## ADDED Requirements

### Requirement: PageAnalyzer outputs full-screen normalized coordinates
The `PageAnalyzer` SHALL apply an inverse coordinate transform to all model-returned coordinates, mapping from the cropped+resized image space to the original full-screen normalized space. The transform SHALL use the same crop/resize parameters as the `ImageResizer` call that produced the image sent to the model.

#### Scenario: Item coordinate is transformed from crop space to full screen
- **WHEN** the model returns an item at normalized coordinate `(0.5, 0.3)` on a cropped image (cropTop=0.0625, cropBottom=0.0625, effective height ratio = 0.875)
- **THEN** the output coordinate is `(0.5, 0.3 * 0.875 + 0.0625) = (0.5, 0.325)`

#### Scenario: Popup coordinate is transformed from crop space to full screen
- **WHEN** the model returns a popup coordinate on a cropped image
- **THEN** the popup coordinate is transformed using the same formula as items

#### Scenario: Menu coordinate is transformed from crop space to full screen
- **WHEN** the model returns a menu coordinate on a cropped image
- **THEN** the menu coordinate is transformed using the same formula as items

#### Scenario: Raw path uses RawScreenBuffer dimensions for transform
- **WHEN** `AnalyzeOnceAsync` is called with a `RawScreenBuffer` (raw path)
- **THEN** the original screen width and height are extracted from `RawScreenBuffer` and used to compute the transform

#### Scenario: Fallback path without dimensions skips transform
- **WHEN** `AnalyzeOnceAsync` is called with `byte[]` PNG (fallback path) and no screen dimensions are available
- **THEN** coordinates are returned as-is (no inverse transform) — the existing behavior is preserved

### Requirement: Transform parameters match ImageResizer call site
The inverse transform parameters (`cropTop`, `cropBottom`, `maxWidth`) SHALL be read from the same source as the `ImageResizer` call: environment variables `UNICLAW_IMAGE_CROP_TOP` / `UNICLAW_IMAGE_MAX_WIDTH` with fallback to `ImageResizer.DefaultCropTopRatio` / `ImageResizer.DefaultMaxWidth`. `cropBottom` SHALL equal `cropTop` (symmetric crop).

#### Scenario: Env var overrides default crop ratio
- **WHEN** `UNICLAW_IMAGE_CROP_TOP=0.1` is set
- **THEN** both `cropTop` and `cropBottom` use 0.1 for the inverse transform

#### Scenario: Default crop ratio when no env var
- **WHEN** `UNICLAW_IMAGE_CROP_TOP` is not set
- **THEN** `ImageResizer.DefaultCropTopRatio` (0.0625) is used

### Requirement: YoloBboxes array is transformed to full-screen space
The `PageAnalyzer` SHALL apply the same inverse coordinate transform to the `YoloBboxes` flat array (`ImmutableArray<int>` of [x, y, w, h] quadruplets) before storing in `PageAnalysis`. Each bbox center y-coordinate SHALL be transformed: `y_full = y_center * (1 - cropTop - cropBottom) + cropTop`.

#### Scenario: YOLO bbox center is transformed
- **WHEN** a YOLO bbox has center y = 0.5 and cropTop = cropBottom = 0.0625
- **THEN** the bbox y-values are adjusted so center_y = 0.5 * 0.875 + 0.0625 = 0.5

#### Scenario: Transformed bboxes produce correct ROI selection
- **WHEN** `BuildYoloBboxes` (now pass-through) de-normalizes the bboxes to screen pixels
- **THEN** the resulting `RoiRect` values match the same screen regions as before the transform migration

### Requirement: BuildYoloBboxes is simplified to pass-through de-normalization
After `PageAnalyzer` outputs full-screen normalized YOLO bboxes, `InterceptionHandler.BuildYoloBboxes` SHALL be simplified to only perform de-normalization (`x_px = x_norm * screenW, y_px = y_norm * screenH`) without crop ratio arithmetic or env var parsing. The method SHALL NOT duplicate the `UNICLAW_IMAGE_CROP_TOP` / `UNICLAW_IMAGE_MAX_WIDTH` environment variable parsing.

#### Scenario: BuildYoloBboxes no longer parses crop env vars
- **WHEN** `BuildYoloBboxes` is called with full-screen normalized bboxes
- **THEN** it performs only `x * screenW, y * screenH` de-normalization without reading `UNICLAW_IMAGE_CROP_TOP`

### Requirement: Python vision server crop defaults to zero
The Python vision server (`tools/local_vision/server.py`) SHALL change the default values of `_CROP_TOP` and `_CROP_BOTTOM` from `0.0625` to `0.0`. Environment variable overrides (`UNICLAW_IMAGE_CROP_TOP` / `UNICLAW_IMAGE_CROP_BOTTOM`) and label-mapping config overrides SHALL remain functional.

#### Scenario: Default no-crop preserves full image
- **WHEN** no env var or config override is set
- **THEN** `_preprocess` performs no crop (top_px = 0, bottom_px = 0) and the full C#-sent image is processed

#### Scenario: Env var override still works
- **WHEN** `UNICLAW_IMAGE_CROP_TOP=0.05` is set
- **THEN** the server still applies that crop ratio, overriding the new default of 0.0
