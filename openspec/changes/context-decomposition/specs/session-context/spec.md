# Session Context Specification

## ADDED Requirements

### Requirement: Session context tracks macro session state
The system SHALL provide a `SessionContext` class that encapsulates macro session state including trace ID, global FSM state, and device/AI configuration.

#### Scenario: Session context initialization
- **WHEN** a `SessionContext` is created with traceId
- **THEN** the context initializes with TraceId set, GlobalState.Idle, and null for optional configuration

### Requirement: Trace identity
The system SHALL maintain a trace ID that identifies the traversal session.

#### Scenario: Trace ID is immutable
- **WHEN** `SessionContext` is created
- **THEN** TraceId is set and cannot be changed

#### Scenario: Read-only trace ID access
- **WHEN** consumer accesses `Session.TraceId`
- **THEN** system returns the trace ID string

### Requirement: Global FSM state
The system SHALL track the macro FSM state (Idle, Initializing, Traversing, Paused, Error, Recovering, Completed, Terminated).

#### Scenario: Global state transitions
- **WHEN** FSM transitions via setting `SessionContext.GlobalState` property
- **THEN** GlobalState updates to the new value

#### Scenario: Read-only global state access via interface
- **WHEN** consumer holds `ISessionContext` reference
- **THEN** GlobalState property is read-only (getter only on interface, setter on concrete class)

#### Scenario: Write access on concrete class
- **WHEN** FSM needs to update global state
- **THEN** setter is available on `SessionContext` concrete class

### Requirement: Device experience configuration
The system SHALL track the device experience level for the session.

#### Scenario: Set device experience
- **WHEN** device experience is set via `SessionContext.DeviceExperience` property
- **THEN** the value is updated (typically set once at session start)

#### Scenario: Read-only device experience access
- **WHEN** consumer accesses `Session.DeviceExperience`
- **THEN** system returns the current value (nullable)

### Requirement: AI provider configuration
The system SHALL track the AI provider used for the session.

#### Scenario: Set AI provider
- **WHEN** AI provider is set via `SessionContext.AIProvider` property
- **THEN** the value is updated (typically set once at session start)

#### Scenario: Read-only AI provider access
- **WHEN** consumer accesses `Session.AIProvider`
- **THEN** system returns the current value (nullable)

### Requirement: Read-only interface isolation
The system SHALL provide `ISessionContext` interface with only read-only property getters.

#### Scenario: Interface exposes no mutation methods
- **WHEN** consumer holds `ISessionContext` reference
- **THEN** only read-only properties are accessible (TraceId, GlobalState getter, DeviceExperience, AIProvider)

#### Scenario: GlobalState setter only on concrete class
- **WHEN** FSM needs to update GlobalState
- **THEN** they must hold `SessionContext` concrete class reference (per D-7, setter not on interface)
