## Purpose

Defines how the perception pipeline selects its OCR recognition (rec) model from declared
configuration, eliminating the current config drift (declared `ocr.language=en` while an
unparameterized constructor loads the Chinese PP-OCRv4 model), and establishes the
managed-artifact regime for OCR model weights (content-addressed, config-referenced).
Behavior contract only — no implementation detail.

## ADDED Requirements

### Requirement: Declared OCR language determines the loaded rec model

The declared OCR language (e.g. `ocr.language`) SHALL determine which rec model is loaded;
an unparameterized default that ignores the declaration is prohibited.

#### Scenario: Declared English loads an English rec model

- **WHEN** the perception config declares `ocr.language=en`
- **THEN** the loaded rec model is an English-capable model (not the Chinese model)

#### Scenario: Unsupported language fails closed

- **WHEN** the config declares an unsupported `ocr.language`
- **THEN** OCR backend initialization fails with an explicit error and SHALL NOT silently
  fall back to a default model

### Requirement: OCR model weights are managed, content-addressed artifacts

OCR model weights (ONNX + dictionary) SHALL be introduced as managed artifacts keyed by
content hash (SHA-256) and referenced from config; they SHALL NOT be implicitly carried by
pip dependencies.

#### Scenario: Artifact is content-addressed and referenced

- **WHEN** a new OCR rec model is introduced
- **THEN** its file name, SHA-256, language and purpose are recorded and the
  ConfigManifest references it by artifact identity

#### Scenario: Unregistered weights are rejected

- **WHEN** an OCR weight file is not registered as a managed artifact
- **THEN** the service refuses to load it and reports the unregistered artifact instead of
  using it silently

### Requirement: Normalization applies before fusion consumers

OCR token output SHALL pass through the text-normalization contract
(see `perception/ocr-text-normalization`) before reaching fusion/downstream consumers;
switching rec models SHALL NOT bypass this layer.

#### Scenario: Normalized output reaches consumers

- **WHEN** any rec model produces tokens
- **THEN** fusion consumers receive the normalized token text per the
  ocr-text-normalization contract

## Constraints

- YOLO detection, fusion logic, candidate/behavior schema are unchanged.
- No new pip runtime dependencies (reuse the RapidOCR 1.4.4 model-override capability).
- Model introduction is governance-side work: new artifact registration follows the
  procedure defined here and SHALL NOT be bypassed by implementers.