## MODIFIED Requirements

### Requirement: PlanCompiler provides deterministic IntentSlots-to-TraversalPlan mapping

`PlanCompiler` SHALL be a sealed class that deterministically maps `IntentSlots` to `TraversalPlan` without AI dependency. `_validate_slots` SHALL validate three categories: (1) `target_app` non-empty, (2) **scope/target combination legality**, and (3) depth legality ≥ 0.

#### Scenario: _validate_slots rejects invalid scope values
- **WHEN** `PlanCompiler.compile(slots)` is called with `scope` set to a value not in TEMPLATE_SETS keys ("full_interaction", "menu_only", "safe_mode", "read_only", "target_path")
- **THEN** `_validate_slots` SHALL throw `DomainValidationException` naming `scope` as the illegal field
- **AND** SHALL NOT silently produce a null result from `BuildDynamicRules`

#### Scenario: _validate_slots rejects target_path scope without target
- **WHEN** `PlanCompiler.compile(slots)` is called with `scope = "target_path"` and `target` is null or empty
- **THEN** `_validate_slots` SHALL throw `DomainValidationException` naming `target` as the illegal field
