# Spec: perception-actionable-toggle-evidence

> Perception capability to produce actionable toggle evidence for existing Binding + StateBeliefReducer.

## ADDED Requirements

### Requirement: Toggle inference from structural evidence

When YOLO does not detect control element labels, the fusion engine SHALL attempt to infer toggle type from deterministic structural/geometric evidence. Inference SHALL be evidence-based and SHALL NOT rely on text content, target names, or semantic knowledge.

#### Scenario: Toggle inferred from right-side compact geometry

- **WHEN** a candidate text element has a compact, right-side aligned element with aspect ratio and position consistent with a Settings toggle switch
- **THEN** the fusion engine SHALL emit `type = "switch"` for the inferred toggle and SHALL associate it with the label row

#### Scenario: Insufficient evidence for toggle inference

- **WHEN** no compatible right-side compact element exists, or the evidence is ambiguous
- **THEN** the fusion engine SHALL NOT emit a toggle type (no false positive)

### Requirement: Switch state inference

For inferred toggle elements, the fusion engine SHALL determine switch state from visual evidence (knob position, brightness distribution).

#### Scenario: OFF toggle

- **WHEN** a toggle element has visual evidence of the knob in the left position (OFF)
- **THEN** the fusion engine SHALL emit `switch_state = false`

#### Scenario: ON toggle

- **WHEN** a toggle element has visual evidence of the knob in the right position (ON)
- **THEN** the fusion engine SHALL emit `switch_state = true`

#### Scenario: Ambiguous state

- **WHEN** visual state evidence is insufficient to determine ON/OFF
- **THEN** the fusion engine SHALL emit `switch_state = null` (UNKNOWN)

### Requirement: Canonical type mapping

The fusion engine SHALL use the existing label mapping vocabulary. Inferred toggles SHALL be emitted as `type = "switch"` (which maps to `"toggle"` via the label mapping). The C# adapter SHALL NOT be modified.

#### Scenario: Inferred toggle type -> "switch"

- **WHEN** a toggle is inferred from structural evidence
- **THEN** the fusion engine SHALL emit `type = "switch"` for the inferred toggle candidate
- **AND** the label mapping SHALL convert it to `perception_type = "toggle"` for the adapter

### Requirement: Row association

Inferred toggle elements SHALL be associated with their label row via same-row geometry (vertical overlap). Each toggle SHALL be associated with exactly one label row. Ambiguous cases SHALL fail closed (no association).

#### Scenario: Toggle associated with correct label row

- **WHEN** a toggle is inferred near a text label row
- **THEN** the toggle SHALL be associated with that label row via vertical overlap
- **AND** the association SHALL be recorded in the candidate evidence

### Requirement: Single perception pass

The normal path SHALL use exactly one screenshot, one YOLO pass, one OCR pass, and one fusion pass. No second screenshot, second model invocation, or LLM/VLM.

#### Scenario: Normal toggle inference uses one pass

- **WHEN** a screenshot is processed for toggle inference
- **THEN** exactly one YOLO pass, one OCR pass, and one fusion pass SHALL be used
- **AND** no second screenshot or model invocation SHALL occur

### Requirement: Test matrix

The implementation SHALL pass the following deterministic falsifier tests.

#### PER-T1: OFF toggle produces actionable toggle evidence

- **WHEN** a fixture contains a label with an associated toggle in OFF state
- **THEN** the fusion engine SHALL produce `type = "switch"`, `switch_state = false`, and correct bounds
- **AND** through Binding → StateBeliefReducer, the state SHALL be `false`

#### PER-T2: ON toggle produces actionable toggle evidence

- **WHEN** a fixture contains a label with an associated toggle in ON state
- **THEN** the fusion engine SHALL produce `type = "switch"`, `switch_state = true`
- **AND** through Binding → StateBeliefReducer, the state SHALL be `true`

#### PER-T3: Ambiguous state

- **WHEN** a toggle geometry is identified but visual state evidence is insufficient
- **THEN** the fusion engine SHALL emit `switch_state = null`

#### PER-T4: Multiple rows with toggles

- **WHEN** a fixture contains multiple labels each with their own toggle
- **THEN** each toggle SHALL be associated with its corresponding label row only

#### PER-T5: Unrelated nearby control

- **WHEN** a label has a nearby candidate that does NOT satisfy row/control evidence
- **THEN** no false association SHALL be made

#### PER-T6: Text-only row

- **WHEN** a fixture contains a label with no compatible toggle evidence
- **THEN** the candidate SHALL remain as `type = "text_block"` (non-actionable)

#### PER-T7: Observation locality

- **WHEN** fresh observations are obtained
- **THEN** old Index and Bounds SHALL NOT remain actionable

#### PER-T8: Freshness

- **WHEN** a post-scroll fresh observation is obtained
- **THEN** toggle binding SHALL derive from the current frame only

#### PER-T9: No scenario leakage

- **WHEN** searching production code for scenario-specific names
- **THEN** no `AutomaticSystemUpdates`, `DeveloperOptions`, `ota_disable_automatic_update`, or specific Android row names SHALL exist in production code

#### PER-T10: Readback is not perception

- **WHEN** Android settings keys are available
- **THEN** they SHALL NOT determine `PerceptionType`, `SwitchState`, `Binding`, or `StateBelief`

#### PER-T11: Single analysis pass

- **WHEN** processing a screenshot
- **THEN** exactly one YOLO, one OCR, one fusion pass SHALL be used

#### PER-T12: Zero cognitive models

- **WHEN** processing perception
- **THEN** LLM and VLM calls SHALL be zero
