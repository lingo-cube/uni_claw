## ADDED Requirements

### Requirement: SubsystemBoundaryGuardTests test class

ArchitectureGuardTests.cs SHALL include a new test class `SubsystemBoundaryGuardTests` that validates subsystem boundary consistency for TraversalRuntimeContext.

#### Scenario: SubsystemBoundaryGuardTests coexists with existing guard classes
- **WHEN** all ArchitectureGuardTests run
- **THEN** SubsystemBoundaryGuardTests MUST pass alongside EnumValueGuardTests, DependencyDirectionGuardTests, NamespaceIsolationGuardTests, and CodingConventionGuardTests

#### Scenario: SubsystemBoundaryGuardTests validates field counts per subsystem
- **WHEN** `SubsystemBoundaryGuardTests.TraversalRuntimeContext_FieldCountsPerSubsystem` runs
- **THEN** it MUST assert NavigationContext-attributed fields count equals 12
- **THEN** it MUST assert ErrorContext-attributed fields count equals 5
- **THEN** it MUST assert SessionContext-attributed fields count equals 5
- **THEN** it MUST assert ProgressContext-attributed fields count equals 5
- **THEN** it MUST assert CacheContext-attributed fields count equals 4 (excluding Phase 3 reserved)
