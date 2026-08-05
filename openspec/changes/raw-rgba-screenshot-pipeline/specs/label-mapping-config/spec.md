## MODIFIED Requirements

### Requirement: Label mapping configuration schema

The `label-mapping.json` file SHALL conform to schema `uniclaw.labelMapping.v1`. It SHALL contain:
- `schema`: string, always `"uniclaw.labelMapping.v1"`
- `mappings`: object mapping YOLO normalized label strings → AI type strings (e.g., `"switch": "toggle"`, `"button": "menu_item"`)
- `nonItemLabels`: array of label strings that set `is_popup` flags rather than becoming items (e.g., `["popup"]`)
- `spatial`: object with `level1MaxY` (float, top tab bar Y threshold), `edgeThreshold` (float, bottom-edge threshold for `candidatesNearBottom`), `roiPadding` (object with `x`, `y`, `minPx`, `maxPx` for ROI crop padding), and `preprocessing` (object with `maxWidth` (int), `cropTopRatio` (float), `cropBottomRatio` (float) for raw RGBA pipeline image preprocessing)
- `detection`: object with `confidence` (float, YOLO detection confidence threshold)

#### Scenario: Valid config with preprocessing section parsed successfully

- **WHEN** `label-mapping.json` with all required fields including `spatial.preprocessing` is loaded
- **THEN** C# `LabelMappingConfig` deserializes without error, and Python `json.loads` succeeds

#### Scenario: Default mappings cover standard YOLO labels

- **WHEN** the default mapping file is loaded
- **THEN** `"button"`, `"list_item"`, `"tab"`, `"icon"`, `"toolbar"`, `"back"` all map to `"menu_item"`; `"switch"` and `"checkbox"` map to `"toggle"`; `"input"` maps to `"input"`; `"slider"` maps to `"slider"`; `"text_block"` maps to `"text"`

#### Scenario: Default preprocessing values

- **WHEN** `spatial.preprocessing` is present in `label-mapping.json`
- **THEN** `maxWidth` is 720, `cropTopRatio` is 0.0625, and `cropBottomRatio` is 0.0625

## ADDED Requirements

### Requirement: Python reads preprocessing config at startup

Python `server.py` SHALL read `spatial.preprocessing` from `label-mapping.json` in its FastAPI lifespan. It SHALL extract `maxWidth`, `cropTopRatio`, and `cropBottomRatio` for `_preprocess()`. Environment variables `UNICLAW_IMAGE_MAX_WIDTH`, `UNICLAW_IMAGE_CROP_TOP`, `UNICLAW_IMAGE_CROP_BOTTOM` SHALL override the config values when set.

#### Scenario: Python uses preprocessing values from config

- **WHEN** `label-mapping.json` has `spatial.preprocessing.maxWidth: 800`
- **THEN** `_preprocess` resizes images to max width 800 (unless overridden by env)

#### Scenario: Environment variable overrides config

- **WHEN** `UNICLAW_IMAGE_MAX_WIDTH=640` is set and `label-mapping.json` has `spatial.preprocessing.maxWidth: 720`
- **THEN** `_preprocess` uses 640 as max width
