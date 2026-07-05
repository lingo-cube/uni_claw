## ADDED Requirements

### Requirement: Value-count guard assertions for all locked enums

Phase 2.1 SHALL add defensive `Enum.GetValues<X>().Length == N` assertion tests for all 10 enums whose value counts are locked by the design document. These guards SHALL prevent accidental addition of enum values in future development.

#### Scenario: TraversalState has exactly 8 values
- **WHEN** `Enum.GetValues<TraversalState>().Length` is evaluated
- **THEN** the result SHALL equal 8 (NodeSelect, PreconditionCheck, Execute, ResultVerify, Branch, FrameComplete, ErrorHandling, PopupHandling — DynamicMatch excluded per D-1)

#### Scenario: GlobalState has exactly 8 values
- **WHEN** `Enum.GetValues<GlobalState>().Length` is evaluated
- **THEN** the result SHALL equal 8 (Idle, Initializing, Traversing, Paused, Error, Recovering, Completed, Terminated)

#### Scenario: NodeType has exactly 8 values
- **WHEN** `Enum.GetValues<NodeType>().Length` is evaluated
- **THEN** the result SHALL equal 8 (Container, LeafSwitch, LeafSlider, LeafAction, LeafInfo, Screen, Action, Target)

#### Scenario: ErrorType has exactly 6 values
- **WHEN** `Enum.GetValues<ErrorType>().Length` is evaluated
- **THEN** the result SHALL equal 6 (Crash, Permission, Timeout, Network, UiElement, Unknown)

#### Scenario: ErrorStrategy has exactly 5 values
- **WHEN** `Enum.GetValues<ErrorStrategy>().Length` is evaluated
- **THEN** the result SHALL equal 5 (Retry, Backtrack, Skip, Continue, Abort)

#### Scenario: PopupType has exactly 5 values
- **WHEN** `Enum.GetValues<PopupType>().Length` is evaluated
- **THEN** the result SHALL equal 5 (Permission, Error, Ad, Dialog, Unknown)

#### Scenario: DismissStrategy has exactly 4 values
- **WHEN** `Enum.GetValues<DismissStrategy>().Length` is evaluated
- **THEN** the result SHALL equal 4 (AutoClose, Back, WaitTimeout, AutoCloseOrBack)

#### Scenario: UrgencyLevel has exactly 3 values (D-11)
- **WHEN** `Enum.GetValues<UrgencyLevel>().Length` is evaluated
- **THEN** the result SHALL equal 3 (Low, Medium, High)
- **NOTE**: Critical was removed as unreachable dead value (→ D-11)

#### Scenario: BlockingType has exactly 3 values
- **WHEN** `Enum.GetValues<BlockingType>().Length` is evaluated
- **THEN** the result SHALL equal 3 (Modal, NonModal, Toast)

#### Scenario: FallbackAction has exactly 4 values
- **WHEN** `Enum.GetValues<FallbackAction>().Length` is evaluated
- **THEN** the result SHALL equal 4 (Back, AutoEscape, Skip, Abort)
