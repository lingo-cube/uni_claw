# perception-actionable-toggle-evidence-reality-repair Specification

## Purpose
TBD - created by archiving change perception-actionable-toggle-evidence-reality-repair. Update Purpose after archive.

## Requirements

### Requirement: Raw-pixel toggle candidate generation

The Python Perception fusion engine SHALL generate toggle candidates from raw screenshot pixels when YOLO does not provide control-class detections. The generation SHALL be based on generic structural/geometric evidence and SHALL NOT require YOLO to have emitted an icon/switch/toggle candidate.

#### Scenario: Developer Options page with text_block-only YOLO output

- **WHEN** the YOLO model outputs only `text_block` candidates on a settings page
- **THEN** the fusion engine SHALL scan the raw pixels in the right-side region of each text row
- **AND** SHALL generate a candidate with type="switch" when a toggle-like structure is found

#### Scenario: Text-only row without toggle

- **WHEN** a text row has no compatible right-side toggle-like structure in the raw pixels
- **THEN** the fusion engine SHALL NOT generate a toggle candidate

### Requirement: Single screenshot, single pass

The repair SHALL NOT capture a second screenshot, run YOLO twice, run OCR twice, or invoke LLM/VLM.

#### Scenario: Normal path uses one perception pass

- **WHEN** a screenshot is processed
- **THEN** exactly one YOLO pass, one OCR pass, and one raw-pixel fusion pass SHALL occur
- **AND** no second screenshot or model invocation SHALL occur

### Requirement: Canonical type mapping

Inferred toggle candidates SHALL use the canonical Python type "switch", which maps to Runtime-facing PerceptionType="toggle" via existing label mapping.

#### Scenario: Inferred toggle emits canonical type

- **WHEN** a toggle candidate is generated from raw pixels
- **THEN** the candidate SHALL have type="switch"
- **AND** the existing label mapping SHALL convert it to "toggle" for the C# adapter

### Requirement: Switch state remains C# authority

The Python `switch_state` field SHALL remain non-authoritative. The C# ImageSwitchStateProvider SHALL remain the sole producer of SwitchState=true/false/null from current screenshot pixels.

#### Scenario: Python state placeholder not used by Runtime

- **WHEN** a toggle candidate is generated with switch_state=None in Python
- **THEN** the C# ImageSwitchStateProvider SHALL read the actual state from the same screenshot pixels
- **AND** the Python switch_state value SHALL NOT be used as Runtime truth

### Requirement: Scenario leakage

Production code SHALL NOT contain target-name, page-name, or scenario-specific rules.

#### Scenario: No scenario names in production

- **WHEN** searching production code for scenario-specific names
- **THEN** no DeveloperOptions, AutomaticSystemUpdates, WiFi, or other target names SHALL be present
