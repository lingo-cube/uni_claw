## 1. Source Annotations

- [x] 1.1 Add subsystem attribution comments to all 26 core mutable fields in TraversalRuntimeContext.cs (each field gets `// <SubsystemName>` comment per canonical table)
- [x] 1.2 Add `// CacheContext (Phase 3)` annotation to `_scrollHandler` and `_currentSnapshot` reserved fields
- [x] 1.3 Add subsystem attribution comment to `CurrentFrame` property (NavigationContext) and `_visitedChildrenReadOnly` lazy field (NavigationContext)

## 2. Guard Tests

- [x] 2.1 Add `SubsystemBoundaryGuardTests` class to ArchitectureGuardTests.cs with `TraversalRuntimeContext_FieldCountsPerSubsystem` test asserting: NavigationContext=12, ErrorContext=5, SessionContext=4, ProgressContext=5, CacheContext=2 (core)
- [x] 2.2 Verify all 4 existing guard test classes + 2 new tests pass (dotnet test → 605 pass)

## 3. Documentation Updates

- [x] 3.1 Update `docs/system/layers/state-machine.md` §5 — replace flat 26-field listing with canonical subsystem-attributed table (5 subsystems × fields + rationale for resolved ambiguities)
- [x] 3.2 Add D-15 decision entries to `docs/system/decisions/log.md` — 5 subsystem canonical names + 10 ambiguity resolutions with rationale
- [x] 3.3 Update `docs/refactor/20-b-refactoring-roadmap-design.md` §5 — remove `?` ambiguity markers, note D-15 completed with canonical table as authoritative source

## 4. Verification

- [x] 4.1 Run `dotnet test` — 605 tests pass including new SubsystemBoundaryGuardTests
- [x] 4.2 Verify no ambiguity markers (`?`) remain in any documentation referencing TraversalRuntimeContext field attribution
