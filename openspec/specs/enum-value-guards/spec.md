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

---

### Requirement: EnumValueGuardTests includes SpanType_Has11Values

EnumValueGuardTests SHALL be extended with a `[Fact]` test `SpanType_Has11Values` that asserts `Enum.GetValues<SpanType>().Length == 11`. This MUST be added alongside the existing 10 Phase2 enum tests and 2 Phase1 Domain enum tests.

#### Scenario: SpanType value count locked at 11
- **WHEN** `Enum.GetValues<SpanType>().Length` is queried
- **THEN** it MUST equal 11
- **THEN** any addition or removal of SpanType values MUST fail this CI-blocking test

#### Scenario: SpanType guard test coexists with existing guards
- **WHEN** all EnumValueGuardTests run
- **THEN** the new SpanType_Has11Values test MUST pass alongside all existing 12 enum value tests

---

### Requirement: SubsystemBoundaryGuardTests test class (D-15)

ArchitectureGuardTests.cs SHALL include a new test class `SubsystemBoundaryGuardTests` that validates subsystem boundary consistency for TraversalRuntimeContext via source annotation parsing.

#### Scenario: SubsystemBoundaryGuardTests coexists with existing guard classes
- **WHEN** all ArchitectureGuardTests run
- **THEN** SubsystemBoundaryGuardTests MUST pass alongside EnumValueGuardTests, DependencyDirectionGuardTests, NamespaceIsolationGuardTests, and CodingConventionGuardTests

#### Scenario: SubsystemBoundaryGuardTests validates field counts per subsystem
- **WHEN** `SubsystemBoundaryGuardTests.TraversalRuntimeContext_FieldCountsPerSubsystem` runs
- **THEN** it MUST assert NavigationContext-attributed fields count equals 12 (10 core + CurrentFrame + _visitedChildrenReadOnly)
- **THEN** it MUST assert ErrorContext-attributed fields count equals 5 (_failedNodes, _consecutiveErrors, _retryCount, _lastError, _exceptionChain)
- **THEN** it MUST assert SessionContext-attributed fields count equals 4 (_traceId, _globalState, _deviceExperience, _aiProvider)
- **THEN** it MUST assert ProgressContext-attributed fields count equals 5 (_stepCount, _maxDepth, _completionPolicy, _actionHistory, _waitAfterActionMs)
- **THEN** it MUST assert CacheContext-attributed fields count equals 2 (_pageCache, _cacheValid, excluding Phase 3 reserved slots)
- **THEN** total attributable fields (excluding Phase 3 reserved) SHALL equal 28 (26 core private + CurrentFrame + _visitedChildrenReadOnly)

#### Scenario: SubsystemBoundaryGuardTests validates Phase 3 reserved annotations
- **WHEN** `SubsystemBoundaryGuardTests.TraversalRuntimeContext_Phase3ReservedFields_AnnotatedAsCacheContext` runs
- **THEN** it MUST assert exactly 2 fields annotated with "CacheContext (Phase 3)" (_scrollHandler, _currentSnapshot)
