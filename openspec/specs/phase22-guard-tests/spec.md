## ADDED Requirements

### Requirement: NamespaceIsolationGuardTests enforce C-3 Domain sub-domain zero cross-import

ArchitectureGuardTests.cs SHALL contain a `NamespaceIsolationGuardTests` inner class with a `[Fact]` test `Domain_Subdomains_ZeroCrossImport` that scans all `.cs` files under `Domain/Vision/`, `Domain/Content/`, and `Domain/Common/` and asserts zero cross-domain `using` statements. Vision files MUST NOT `using UniClaw.Core.Domain.Content` or `UniClaw.Core.Domain.Common`. Content files MUST NOT `using UniClaw.Core.Domain.Domain.Vision` or `UniClaw.Core.Domain.Common`. Common files MUST NOT `using UniClaw.Core.Domain.Domain.Vision` or `UniClaw.Core.Domain.Content`. Exception: `Domain/Mappings/` (the bridge) CAN reference Vision and Content.

#### Scenario: Vision file does not import Content namespace
- **WHEN** a `.cs` file under `Domain/Vision/` is scanned
- **THEN** it MUST NOT contain `using UniClaw.Core.Domain.Content` or `using UniClaw.Core.Domain.Common`

#### Scenario: Content file does not import Vision namespace
- **WHEN** a `.cs` file under `Domain/Content/` is scanned
- **THEN** it MUST NOT contain `using UniClaw.Core.Domain.Domain.Vision` or `using UniClaw.Core.Domain.Common`

#### Scenario: Mappings file CAN import Vision and Content
- **WHEN** a `.cs` file under `Domain/Mappings/` is scanned
- **THEN** it MAY contain `using UniClaw.Core.Domain.Domain.Vision` and `using UniClaw.Core.Domain.Content` (the bridge is the only cross-domain import)

### Requirement: NamespaceIsolationGuardTests enforce C-4 FSM independence

ArchitectureGuardTests.cs SHALL contain a `NamespaceIsolationGuardTests` inner class with a `[Fact]` test `FSMs_DoNotShareTypes` that checks `TraversalFSM.cs` and `GlobalFSM.cs`. TraversalFSM MUST NOT reference GlobalFSM-specific types (GlobalState enum, GlobalTransition). GlobalFSM MUST NOT reference TraversalFSM-specific types (TraversalState enum, TraversalTransition). Exception: both MAY reference `ITraversalContext` (coordination interface, not FSM type). Note: D-7 deviation (GlobalState setter on ITraversalContext) is NOT validated by this test — that is Phase 3 scope.

#### Scenario: TraversalFSM does not reference GlobalState type
- **WHEN** `TraversalFSM.cs` is scanned
- **THEN** it MUST NOT contain `using UniClaw.Core.StateMachine` references to GlobalState or GlobalTransition types defined in GlobalFSM.cs

#### Scenario: GlobalFSM does not reference TraversalState type
- **WHEN** `GlobalFSM.cs` is scanned
- **THEN** it MUST NOT contain references to TraversalState or TraversalTransition types defined in TraversalFSM.cs

#### Scenario: Both FSMs MAY reference ITraversalContext
- **WHEN** either FSM file is scanned
- **THEN** references to `ITraversalContext` are NOT flagged as violations (coordination interface is allowed)

### Requirement: CodingConventionGuardTests enforce C-9 sealed record class

ArchitectureGuardTests.cs SHALL contain a `CodingConventionGuardTests` inner class with a `[Fact]` test `AllRecords_AreSealedRecordClass` that scans all `.cs` files under `Domain/`, `StateMachine/`, `Traversal/`, and `Graph/` for `record class` definitions and asserts each is preceded by `sealed`. Exception: `TraversalRuntimeContext` is `sealed class` (not record — 26 mutable fields).

#### Scenario: Domain record is sealed
- **WHEN** a `record class` definition is found under `Domain/`
- **THEN** the definition MUST be preceded by `sealed`

#### Scenario: TraversalRuntimeContext exception
- **WHEN** `TraversalRuntimeContext` is found
- **THEN** it MUST be `sealed class` (not `sealed record class`) — this is an allowed exception

### Requirement: CodingConventionGuardTests enforce C-10 DomainValidationException unified validation

ArchitectureGuardTests.cs SHALL contain a `CodingConventionGuardTests` inner class with a `[Fact]` test `Domain_UsesDomainValidationException` that scans all `.cs` files under `Domain/` and asserts NO `throw new InvalidOperationException` or `throw new ArgumentException`. Note: `Domain.Mappings/ElementTypeMapper` uses graceful fallback (IsValid notification, no throw) — this is correct and not flagged.

#### Scenario: Domain file does not throw InvalidOperationException
- **WHEN** a `.cs` file under `Domain/` is scanned
- **THEN** it MUST NOT contain `throw new InvalidOperationException`

#### Scenario: Domain file does not throw ArgumentException
- **WHEN** a `.cs` file under `Domain/` is scanned
- **THEN** it MUST NOT contain `throw new ArgumentException`

#### Scenario: ElementTypeMapper graceful fallback is not flagged
- **WHEN** `Domain/Mappings/ElementTypeMapper.cs` is scanned
- **THEN** it MAY use `IsValid` notification pattern without `throw` (graceful fallback is correct per C-10)
