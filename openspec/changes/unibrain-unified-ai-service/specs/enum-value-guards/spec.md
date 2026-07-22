## ADDED Requirements

### Requirement: UniBrain guard tests enforce zero upward references

EnumValueGuardTests (extended as ArchitectureGuardTests) SHALL include the following guard tests:

1. `UniBrain_DoesNotReferenceStateMachine`: Asserts that no type in `UniClaw.Core.UniBrain` namespace references any type in `UniClaw.Core.StateMachine` namespace.
2. `UniBrain_DoesNotReferenceTraversal`: Asserts that no type in `UniClaw.Core.UniBrain` namespace references any type in `UniClaw.Core.Traversal` namespace.
3. `IUniBrain_Has3SubInterfaces`: Asserts that IUniBrain interface has exactly 3 properties of types IPageAnalyzer, ITraversalAdvisor, ITextUnderstanding.
4. `IScreenStateProvider_Has4Methods`: Asserts that IScreenStateProvider interface has exactly 4 public methods (HasScroll, GetScrollProgress, IsEndOfList, GetScrollSwipeConfig).
5. `StateMachine_ReferencesUniBrainForIUniBrain`: Acknowledges upward reference StateMachine→UniBrain for IUniBrain injection. Test verifies that StateMachine references UniBrain only through IUniBrain interface, not concrete implementation types.
6. `Traversal_ReferencesUniBrainForIUniBrain`: Acknowledges upward reference Traversal→UniBrain for IUniBrain + IScreenStateProvider injection. Test verifies that Traversal references UniBrain only through IUniBrain interface, not concrete implementation types.

#### Scenario: UniBrain namespace has zero StateMachine references
- **WHEN** ArchitectureGuardTests scans UniBrain namespace
- **THEN** no type in UniBrain namespace imports or references StateMachine namespace types

#### Scenario: IUniBrain facade has exactly 3 sub-interface properties
- **WHEN** ArchitectureGuardTests inspects IUniBrain interface
- **THEN** exactly 3 properties exist: PageAnalyzer (IPageAnalyzer), Advisor (ITraversalAdvisor), Text (ITextUnderstanding)

#### Scenario: IScreenStateProvider has exactly 4 methods
- **WHEN** ArchitectureGuardTests inspects IScreenStateProvider interface
- **THEN** exactly 4 public methods exist: HasScroll, GetScrollProgress, IsEndOfList, GetScrollSwipeConfig
