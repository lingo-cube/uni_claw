# screen-state-provider delta Specification

## ADDED Requirements

### Requirement: VisionScreenStateProvider as new IScreenStateProvider implementation

A new `VisionScreenStateProvider` SHALL be added to `UniClaw.Core/Traversal/` implementing `IScreenStateProvider`. It SHALL NOT implement `IObservableScreenStateProvider`. It SHALL delegate `HasScroll()` and `IsEndOfList()` to a `Func<PageAnalysis?>` injected at construction. It SHALL NOT modify the `IScreenStateProvider` interface — the 4-method lock SHALL remain unbroken.

This implementation SHALL coexist with existing `DefaultScreenStateProvider` (all-virtual defaults), `MockScreenStateProvider` (programmable), and `AdbScreenStateProvider` (UIAutomator-based) — each serving different device scenarios.

#### Scenario: New implementation does not change interface

- **WHEN** `ArchitectureGuardTests.IScreenStateProvider_Has4Methods` runs after `VisionScreenStateProvider` is added
- **THEN** the test passes — 4 methods on the interface, unchanged

#### Scenario: Four implementations coexist

- **WHEN** all `IScreenStateProvider` implementations are enumerated
- **THEN** `DefaultScreenStateProvider`, `MockScreenStateProvider`, `AdbScreenStateProvider`, and `VisionScreenStateProvider` each implement the 4-method interface
