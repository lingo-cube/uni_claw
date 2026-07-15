## ADDED Requirements

### Requirement: ErrorHandler SHALL read per-node ErrorPolicy for recovery strategy

`ErrorStrategySelector.SelectStrategy()` SHALL inspect the current node's `ErrorPolicy` property when it is non-null. When `ErrorPolicy` is present, `MaxRetries` SHALL be taken from `ErrorPolicy.MaxRetries` instead of the hardcoded default, and `ErrorPolicy.OnError` SHALL influence the `StrategyChain` selection. When `ErrorPolicy` is null, the existing hardcoded default behavior SHALL be preserved (backward compatible).

#### Scenario: ErrorPolicy.MaxRetries overrides default
- **WHEN** the current node has `ErrorPolicy { MaxRetries: 5 }`
- **AND** an error triggers `ErrorStrategySelector.SelectStrategy()`
- **THEN** `StrategySelectionContext.MaxRetries` SHALL be 5 (not the default 3)

#### Scenario: Null ErrorPolicy preserves default behavior
- **WHEN** the current node has `ErrorPolicy = null`
- **AND** an error triggers `ErrorStrategySelector.SelectStrategy()`
- **THEN** `StrategySelectionContext.MaxRetries` SHALL be the hardcoded default (3)

#### Scenario: ErrorPolicy.OnError maps to strategy
- **WHEN** the current node has `ErrorPolicy { OnError: Abort }`
- **AND** an error triggers `ErrorStrategySelector.SelectStrategy()`
- **THEN** the selected strategy SHALL be `Abort`
