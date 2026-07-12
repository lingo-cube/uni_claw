# Design: Scroll Simulation Enhancement

## Context

The current C# simulation infrastructure (`StatefulMockVisionService`) returns fixed element sets with static `IsEndOfList` values derived from `PageState.IsComplete`. This design cannot support scrollable list testing scenarios where:

1. Element visibility changes based on scroll position
2. `IsEndOfList` must be dynamically computed based on current progress
3. `HasScroll` indicates whether more content exists
4. Element deduplication is needed across scroll positions

The Python `uni_claw` V7.0 codebase includes scroll simulation in `src/simulation/scroll/`. This design aligns with Python concepts while adding C#-specific enhancements (jump detection, adaptive step sizing).

**Constraints**:
- Use C# `StateFixtureBuilder` pattern (not JSON fixtures)
- Maintain backward compatibility with existing non-scroll tests
- All scroll parameters must be configurable
- Follow UniClaw.Core patterns: sealed records, ImmutableArray, DomainValidationException

**Stakeholders**:
- Simulation test authors (need scroll scenario support)
- TraversalEngine consumers (need scroll integration)
- CI pipeline (all existing tests must pass)

## Goals / Non-Goals

**Goals**:
1. Enable scroll simulation for list traversal testing (3+ screen lists)
2. Track scroll progress, count, and history per page
3. Dynamically compute `HasScroll` and `IsEndOfList` based on position
4. Implement accumulation mode (all segments with `threshold <= progress` visible)
5. Detect and recover from scroll jumps (element discontinuity)
6. Adapt step size based on duplicate element ratio
7. Provide comprehensive test coverage (19 scenarios)

**Non-Goals**:
- Horizontal scrolling (vertical only in Phase 1)
- Nested scrolling containers (single container only)
- Upward scrolling (downward only in Phase 1)
- Fault injection (delay, unresponsiveness)
- Scroll decision in main TraversalEngine flow (deferred to Phase 2)

## Decisions

### Decision 1: Accumulation Mode for Element Visibility

**Choice**: All `ScrollSegment`s with `Threshold <= CurrentProgress` contribute visible elements.

**Rationale**:
- Matches Python V7.0 behavior
- Natural semantic: "as you scroll down, more content appears"
- Simple to implement and verify
- Enables precise control over element appearance

**Alternatives Considered**:
- *Window-based visibility*: Elements visible only within a fixed range around progress. Rejected because adds complexity without clear benefit for simulation testing.

### Decision 2: Element Deduplication Strategy

**Choice**: When same element ID appears in multiple segments, return only the instance from the lowest threshold segment.

**Rationale**:
- Prevents duplicate element visits during traversal
- Matches user expectation (same element = same identity)
- Enables reliable "visited children" tracking
- Lowest threshold preserves original element context

**Implementation**:
```csharp
// Collect all visible elements, then deduplicate by ID
var visible = segments.Where(s => s.Threshold <= progress).SelectMany(s => s.Elements);
var deduplicated = visible.GroupBy(e => e.Id).Select(g => g.OrderBy(e => GetSourceThreshold(e)).First());
```

### Decision 3: Jump Detection as Core Chain Logic

**Choice**: Jump detection (element discontinuity) is part of the ScrollHandler pipeline, not a test validation concern.

**Rationale**:
- Jumps can occur in real scenarios (aggressive step sizing, sparse segments)
- Recovery requires rollback and retry, which is operational logic
- Makes scroll behavior robust rather than brittle
- Statistics tracking helps diagnose scroll efficiency

**Recovery Strategy**:
1. Detect: `BeforeElements` ∩ `AfterElements` = ∅ (both non-empty)
2. Rollback: Restore progress to pre-scroll value
3. Retry: Reduce step size by factor (default 0.5x)
4. Repeat: Until overlap detected or max retries exceeded

### Decision 4: Adaptive Step Calculation

**Choice**: Increase scroll step when duplicate element ratio exceeds threshold (default 70%).

**Rationale**:
- High duplicate ratio = small effective movement = inefficient scrolling
- Adaptive sizing reduces redundant scroll operations
- Configurable to allow tuning per scenario
- Respects `MinSampleSize` to avoid premature optimization

**Formula**:
```
IF (DuplicateRatio >= Threshold) AND (NewElementCount >= MinSampleSize)
    NextStep = Min(CurrentStep * IncreaseFactor, MaxScrollStep)
ELSE
    NextStep = CurrentStep
```

### Decision 5: ScrollHandler 7-step Pipeline

**Choice**: Pipeline architecture (Detect → Classify → Decide → Execute → Verify → Recover → Statistics).

**Rationale**:
- Each step is pure function or side-effect-isolated
- Easy to test individual components
- Clear separation of concerns
- Matches Handler pipeline pattern from `docs/system/patterns/handler-pipeline.md`

**Step Responsibilities**:
1. **Detect**: Determine scrollability (NotScrollable, CanScrollDown, AtBottom, CanScrollUp)
2. **Classify**: Compute progress, max threshold, recommended step
3. **Decide**: Map scrollability to action type (None, ScrollDown, ScrollUp)
4. **Execute**: Perform scroll via Hook Dispatch table
5. **Verify**: Check for jumps via element overlap detection
6. **Recover**: Handle jumps with rollback and retry
7. **Statistics**: Track scroll metrics

### Decision 6: ScrollableMock Services as Extensions

**Choice**: `ScrollableMockVisionService` and `ScrollableMockActionExecutor` are separate classes, not replacements.

**Rationale**:
- Backward compatibility: existing tests use `StatefulMockVisionService` unchanged
- Opt-in: scroll scenarios explicitly use scrollable services
- Clear intent: type name indicates scroll capability
- No breaking changes to existing API surface

**Usage Pattern**:
```csharp
// Non-scroll test (unchanged)
var vision = new StatefulMockVisionService(fixture);

// Scroll test (new)
var vision = new ScrollableMockVisionService(fixture);
```

## Risks / Trade-offs

### Risk 1: Jump Detection False Positives

**Risk**: Initial state (`BeforeElements` empty) triggers false "jump" detection.

**Mitigation**: `OverlapStatus.NoOverlap_BeforeEmpty` is a safe state (not a jump). Only `NoOverlap_BothHaveElements` indicates a true jump.

### Risk 2: Adaptive Step Premature Optimization

**Risk**: Adaptive step increases too early with small sample sizes, causing overshoot.

**Mitigation**: `MinSampleSize` config (default 3) ensures sufficient data before increasing. Step is clamped to `MaxScrollStep` to prevent excessive jumps.

### Risk 3: Progress Accumulation Error

**Risk**: Repeated scroll operations accumulate floating-point error, causing precision issues near boundaries.

**Mitigation**: Use `ProgressEpsilon` (default 0.001) for comparisons. Clamp final progress to `[0.0, MaxThreshold]`.

### Risk 4: Existing Test Breakage

**Risk**: Scroll introduction breaks existing non-scroll tests.

**Mitigation**:
- Scrollable services are opt-in, not replacements
- Existing `StatefulMockVisionService` unchanged
- All scroll features behind `HasScrollData(pageId)` guard

### Trade-off: Step Size vs. Scan Completeness

**Trade-off**: Larger step sizes reduce scroll operations but increase jump risk.

**Mitigation**: Default step (30%) balances efficiency with safety. Configurable per scenario. Jump recovery prevents missed elements.

## Migration Plan

### Phase 1: Data Models (no migration impact)
- Add `src/UniClaw.Core/Simulation/Scroll/` namespace
- Implement core types: `ScrollSegment`, `ScrollState`, `ScrollDataStore`, validation types, config

### Phase 2: Builder Extensions (no migration impact)
- Extend `StateFixtureBuilder` with `.ScrollSegments()` fluent API
- Backward compatible: optional extension

### Phase 3: Mock Services (no migration impact)
- Add `ScrollableMockVisionService` and `ScrollableMockActionExecutor`
- Existing `StatefulMock*` classes unchanged

### Phase 4: ScrollHandler Components (no migration impact)
- Add `src/UniClaw.Core/StateMachine/Scroll/` namespace
- Implement 7-step pipeline components

### Phase 5: FSM Integration (migration impact - opt-in)
- Add scroll check point in `TraversalFSM.HandleBranch()`
- Only active when `ScrollableMockVisionService` is used
- Non-scroll tests unaffected

### Rollback Strategy
All changes are additive. No existing types modified. Rollback = delete new namespaces and revert `HandleBranch()` scroll check.

## Open Questions

1. **Scroll Decision Integration Point**: Should `ScrollHandler.HandleScroll()` be called from `TraversalFSM.HandleBranch()` or from a separate `ScrollDecider` handler component?
   - ** leaning**: HandleBranch is appropriate (when all children visited, check if scroll needed)
   - Resolution needed: Confirm with stakeholder

2. **Default Step Size**: Is 30% appropriate for most scenarios, or should this be scenario-specific?
   - **Leaning**: 30% is good default, configurable via `ScrollHandlerConfig`
   - Resolution: Proceed with configurable default

3. **MinSampleSize for Adaptive Step**: Default 3 elements may be too conservative for short lists.
   - **Leaning**: Keep 3 as default, allow configuration
   - Resolution: Proceed with configurable default
