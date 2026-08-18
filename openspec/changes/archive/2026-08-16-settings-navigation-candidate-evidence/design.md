# Design: Settings Navigation Candidate Evidence

## Two-Layer Evidence Model

### Layer A — Environment / Observation Evidence

Raw structured Android UI facts, sourced from an Android driver/Environment capability such as UIAutomator hierarchy or accessibility node tree.

Candidate raw fields, only where actually available and externally grounded:

- Android widget/class type
- clickable
- checkable
- checked
- enabled
- focusable
- resource-id
- content-description
- parent/child structural facts
- upstream structured action role, if externally derived

These are raw facts, not semantic navigation claims.

### Layer B — Runtime Semantic Evidence

`InteractionAffordanceEvidence`

Classification:

- `NAVIGATION_CANDIDATE`
- `LOCAL_CONTROL`
- `UNKNOWN`

This value answers only:

> What interaction/navigation role is supported by accepted evidence?

It does NOT answer authorization, destination identity, completion, or Goal satisfaction.

## Source Investigation

The proposal requires a real-source investigation before implementation:

- exact Android command/API
- exact data shape
- latency
- emulator availability
- physical device availability
- root/accessibility service requirements
- whether it can be captured alongside the current screenshot observation

If no structured source is actually accessible, implementation must stop with `STRUCTURED_UI_SOURCE_UNAVAILABLE`.

## Correlation

Visual elements and structured Android nodes must be correlated deterministically.

Available keys may include bounds, stable index, resource-id, or structural node identity.

If correlation is ambiguous, the element classification MUST be `UNKNOWN`.

## Settings-Scoped Classification

The mechanism is Settings-scoped. It may use Android Settings widget/Preference structure only where deterministic evidence supports it.

Examples:

- Preference-like navigation row → `NAVIGATION_CANDIDATE`
- SwitchPreference / toggle control → `LOCAL_CONTROL`
- ambiguous interactive row → `UNKNOWN`

`clickable == navigation` is forbidden.

`menu_item == navigation` is forbidden.

## Popup Exclusion

Popup/dialog controls must not become normal Settings child candidates.

Existing obstruction semantics remain authoritative. If a structured node is outside the current Container scope or belongs to a popup/dialog, it must not be classified as a normal Settings navigation candidate.

## Implementation Slices

1. Prove real structured Android UI evidence source.
2. Define minimum external Observation contract delta.
3. Correlate structured evidence to ObservedElement.
4. Add Runtime `InteractionAffordanceEvidence`.
5. Settings-scoped ternary classification.
6. UNKNOWN / local-action safety.
7. SNE-1..SNE-14 production tests.
8. Real Settings/emulator evidence.
9. Regression.
