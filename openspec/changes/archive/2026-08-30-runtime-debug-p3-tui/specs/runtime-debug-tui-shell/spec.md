## ADDED Requirements

### Requirement: Thin TUI shell over the shared core
The TUI SHALL render only view models derived from the Query Core (execution/causal trees, AssetRefs, filter state, diagnosis facts) and SHALL NOT implement correlation, pruning, or analysis locally. The framework (textual) SHALL be confined to the shell module; view models SHALL be stdlib-only and unit-testable without the framework. The Core package SHALL remain framework-free.

#### Scenario: View models derive from core results
- **WHEN** a bundle is opened in the TUI
- **THEN** the asset count, terminal facts, and trace availability SHALL equal the Core projections, and no UI code SHALL re-derive them

#### Scenario: Framework is isolated
- **WHEN** the view-model module is imported in an environment without textual
- **THEN** it SHALL import and operate without the framework present

### Requirement: TUI tree and panel interactions
The TUI SHALL switch between EXECUTION and CAUSAL views, toggle an errors-only filter (FAILED/CANCELLED spine per the execution-tree contract), list AssetRefs, and show a diagnosis panel from the Core (failed spans); all within the same Core contracts used by the CLI.

#### Scenario: Errors-only filter mirrors the core
- **WHEN** errors-only is toggled in the TUI
- **THEN** the visible tree SHALL contain the same FAILED/CANCELLED + ancestor spine as `execution-tree --only-errors`
