# screen-state-provider delta Specification

## MODIFIED Requirements

### Requirement: VisionScreenStateProvider implements IObservableScreenStateProvider

VisionScreenStateProvider SHALL implement both IScreenStateProvider and IObservableScreenStateProvider. The constructor SHALL accept an optional IObservableScreenStateProvider? parameter for UIA redundancy. The interface method count for IScreenStateProvider SHALL remain 4. The RefreshAsync method SHALL be additive to the existing 4 scroll query methods.

#### Scenario: Type implements both interfaces

- **WHEN** VisionScreenStateProvider type is inspected
- **THEN** `typeof(IObservableScreenStateProvider).IsAssignableFrom(typeof(VisionScreenStateProvider))` is true
- **THEN** `typeof(IScreenStateProvider).IsAssignableFrom(typeof(VisionScreenStateProvider))` is true

#### Scenario: ArchitectureGuard 4-method lock preserved

- **WHEN** `ArchitectureGuardTests.IScreenStateProvider_Has4Methods` runs
- **THEN** the test passes — IScreenStateProvider still defines exactly 4 methods
