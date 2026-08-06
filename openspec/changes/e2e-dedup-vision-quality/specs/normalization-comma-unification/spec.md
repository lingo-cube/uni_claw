## ADDED Requirements

### Requirement: Normalize handles comma-space variants
The `ScenarioCompletionVerifier.Normalize()` method SHALL normalize comma-space variants so that `"a,b"`, `"a, b"`, `"a , b"`, and `"a ,b"` all produce the same normalized string. The normalization SHALL treat comma as a token separator equivalent to whitespace for the purposes of identity comparison.

#### Scenario: Comma without space vs comma with space
- **WHEN** `Normalize("Darktheme,fontsize,brightness")` and `Normalize("Darktheme, fontsize, brightness")` are compared
- **THEN** both produce the same normalized output

#### Scenario: Space before comma is normalized
- **WHEN** `Normalize("Bluetooth , pairing")` is called
- **THEN** the result matches `Normalize("Bluetooth, pairing")`

#### Scenario: No comma in input is unchanged behavior
- **WHEN** `Normalize("Dark theme font size")` is called
- **THEN** the result is `"dark theme font size"` (existing whitespace-fold + lowercase behavior preserved)

#### Scenario: Multiple consecutive commas produce consistent form
- **WHEN** `Normalize("a,,b")` is called
- **THEN** the result is `"a, , b"` (each comma becomes a space-separated token — consistent with D-G13 NormalizeItemText behavior)

### Requirement: Normalization is consistent with D-G13 NormalizeItemText
The `Normalize()` output SHALL be consistent with `TraversalEngine.NormalizeItemText()` (D-G13) such that the same input text produces the same normalized key in both the engine (item identity for fingerprinting) and the verifier (entry name for completion checking).

#### Scenario: Same input, same output across both normalizers
- **WHEN** the string `"Bluetooth, pairing"` is passed to both `Normalize()` and `NormalizeItemText()`
- **THEN** both return the same normalized string
