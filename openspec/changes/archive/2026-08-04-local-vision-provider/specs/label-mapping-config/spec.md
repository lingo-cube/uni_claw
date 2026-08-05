# label-mapping-config Specification

## Purpose
Shared configuration file (`tools/local_vision/label-mapping.json`) consumed by both C# `LocalVisionProvider` and Python `server.py`. Defines YOLO label → AI type mappings, spatial parameters, and detection thresholds — single source of truth eliminating dual-threshold divergence.

## ADDED Requirements

### Requirement: Label mapping configuration schema

The `label-mapping.json` file SHALL conform to schema `uniclaw.labelMapping.v1`. It SHALL contain:
- `schema`: string, always `"uniclaw.labelMapping.v1"`
- `mappings`: object mapping YOLO normalized label strings → AI type strings (e.g., `"switch": "toggle"`, `"button": "menu_item"`)
- `nonItemLabels`: array of label strings that set `is_popup` flags rather than becoming items (e.g., `["popup"]`)
- `spatial`: object with `level1MaxY` (float, top tab bar Y threshold), `edgeThreshold` (float, bottom-edge threshold for `candidatesNearBottom`), and `roiPadding` (object with `x`, `y`, `minPx`, `maxPx` for ROI crop padding)
- `detection`: object with `confidence` (float, YOLO detection confidence threshold)

#### Scenario: Valid config parsed successfully

- **WHEN** `label-mapping.json` with all required fields is loaded
- **THEN** C# `LabelMappingConfig` deserializes without error, and Python `json.loads` succeeds

#### Scenario: Default mappings cover standard YOLO labels

- **WHEN** the default mapping file is loaded
- **THEN** `"button"`, `"list_item"`, `"tab"`, `"icon"`, `"toolbar"`, `"back"` all map to `"menu_item"`; `"switch"` and `"checkbox"` map to `"toggle"`; `"input"` maps to `"input"`; `"slider"` maps to `"slider"`; `"text_block"` maps to `"text"`

### Requirement: C# validation at construction

C# `LocalVisionProvider` SHALL load `label-mapping.json` at construction and validate every mapping value against `ElementTypeMapper.IsValidType()`. Any invalid value SHALL throw `DomainValidationException` immediately. The file path SHALL resolve from: constructor parameter → `UNICLAW_LABEL_MAPPING` env var → `"tools/local_vision/label-mapping.json"` default.

#### Scenario: All mapping values are valid AI types

- **WHEN** configuration is loaded and every mapping value passes `ElementTypeMapper.IsValidType()`
- **THEN** construction succeeds

#### Scenario: Invalid mapping value fails construction

- **WHEN** a mapping value (e.g., `"invalid_type"`) fails `ElementTypeMapper.IsValidType()`
- **THEN** `DomainValidationException` is thrown with FieldName set to the invalid value

#### Scenario: Custom path via environment variable

- **WHEN** `UNICLAW_LABEL_MAPPING` is set to `/custom/path.json`
- **THEN** the provider loads configuration from that path

### Requirement: Python reads config at startup

Python `server.py` SHALL read `label-mapping.json` in its FastAPI lifespan (before accepting requests). It SHALL extract `spatial.edgeThreshold` for `candidatesNearBottom` calculation, `detection.confidence` for YOLO threshold, and `spatial.roiPadding` for crop padding. Path SHALL be overridable via `UNICLAW_LABEL_MAPPING` environment variable.

#### Scenario: Python uses edgeThreshold from config

- **WHEN** `label-mapping.json` has `spatial.edgeThreshold: 0.90` and candidates are processed
- **THEN** `candidatesNearBottom` counts candidates with `center.y > 0.90` (not hardcoded 0.92)

#### Scenario: Python uses detection confidence from config

- **WHEN** `label-mapping.json` has `detection.confidence: 0.40`
- **THEN** YOLO detection uses confidence threshold 0.40 (not hardcoded 0.35)

#### Scenario: Python uses ROI padding from config

- **WHEN** `label-mapping.json` has `spatial.roiPadding: {x: 0.15, y: 0.10, minPx: 8, maxPx: 64}`
- **THEN** ROI crop padding is computed as `max(x*width, y*height, minPx)` clamped to `maxPx`

### Requirement: NonItemLabels mechanism

Labels listed in `nonItemLabels` SHALL cause their corresponding YOLO detections to set `is_popup: true` in the output rather than being added to the `items` array. The C# mapping pipeline SHALL check `nonItemLabels` before processing a candidate into an item.

#### Scenario: Popup label excluded from items

- **WHEN** a candidate has `type: "popup"` which is in `nonItemLabels`
- **THEN** `is_popup` is set to true and the candidate does not appear in `items`

### Requirement: Config hash in evidence metadata

The Python server SHALL compute a SHA-256 hash of the `label-mapping.json` file content at startup and include it in the evidence `metadata.configHash` field. This enables C# to verify configuration consistency across the HTTP boundary.

#### Scenario: Config hash included in evidence

- **WHEN** evidence JSON is produced by the Python server
- **THEN** `metadata.configHash` is a 64-character hex string (SHA-256 of label-mapping.json content)
