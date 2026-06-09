## Context

Uni-Claw V6.11 provides mock vision and action services for testing UI traversal, but lacks scrollable list simulation. Current mock services return fixed element sets per page, making it impossible to test scroll scenarios, track scroll state, or inject scroll-related faults.

The GraphTraversalEngine supports declarative traversal and can handle scroll actions (scroll_down, scroll_up), but the mock infrastructure doesn't provide the corresponding scroll simulation capabilities.

**Constraints**:
- Must extend existing `StatefulMockVisionService` and `StatefulMockActionExecutor` without breaking existing tests
- Must maintain backward compatibility with non-scroll test scenarios
- Must align with existing `PageAnalysis.items` and `MenuItem` models (P0 fixes applied)
- Test-only feature—no impact on production runtime or real device execution

**Stakeholders**:
- Test developers writing scroll scenario tests
- CI/CD pipeline running simulation tests
- QA team validating scroll behavior

## Goals / Non-Goals

**Goals:**
- Enable mock services to simulate scrollable lists with progressive element visibility
- Track scroll state (progress, count, history) per page
- Support fault injection (delay, unresponsiveness) for edge case testing
- Provide complete test coverage with 52 tests across 10 scenarios
- Maintain zero breaking changes to existing mock services

**Non-Goals:**
- Real device scroll support (V7.x future work)
- Horizontal scrolling (V7.x future work)
- Nested scroll containers (V7.x future work)
- Gesture simulation (pinch, long press)
- Dynamic content loading ("loading" states)
- Production runtime modifications

## Decisions

### 1. Extend Rather Than Replace Mock Services

**Decision**: Create `ScrollableMockVisionService` and `ScrollableMockActionExecutor` as subclasses of existing `StatefulMockVisionService` and `StatefulMockActionExecutor`.

**Rationale**:
- Preserves existing test behavior—non-scroll tests continue unchanged
- Leverages proven base class functionality (_current_page_id, navigation_history)
- Enables gradual adoption—tests opt-in by using scrollable variants
- Avoids risky changes to widely-used mock infrastructure

**Alternatives Considered**:
- Modify existing mock services directly: Rejected due to high regression risk and potential impact on 100+ existing tests
- Create parallel independent services: Rejected due to code duplication and maintenance burden

### 2. Accumulation Mode for Element Visibility

**Decision**: Implement "accumulation mode" where all elements with `threshold <= progress` are visible.

**Rationale**:
- Matches real scroll behavior—items remain on screen as you scroll
- Simplifies testing—elements don't disappear unpredictably
- Enables efficient deduplication via element IDs
- Clear mental model for test authors

**Alternatives Considered**:
- Window mode (only elements in current viewport): Rejected due to complexity in defining viewport bounds and potential for elements to "flicker" in/out
- Replacement mode (new elements replace old): Rejected as unrealistic—real scrolling doesn't hide previous items

### 3. Scroll Progress as Normalized Float (0.0-1.0)

**Decision**: Track scroll progress as a float between 0.0 (top) and 1.0 (bottom).

**Rationale**:
- Normalized values work across any list length (short lists, long lists)
- Simple arithmetic for scroll deltas and percentages
- Easy to interpret—0.5 means "halfway down"
- Maps cleanly to test expectations

**Alternatives Considered**:
- Pixel-based progress: Rejected due to device-specific resolutions
- Element-count based progress: Rejected as list lengths vary dynamically

### 4. Fault Injection Via State Flags

**Decision**: Support fault injection through mutable state flags (`fail_next_scroll`, `simulate_delay_ms`) on `ScrollState`.

**Rationale**:
- Simple API—just set flags before scrolling
- Enables predictable fault scenarios for testing
- No external dependencies or mocking layers needed
- State resets automatically for one-shot faults

**Alternatives Considered**:
- Separate fault injection service: Rejected as over-engineering for test-only feature
- Config file based faults: Rejected due to complexity in dynamic test scenarios

### 5. Element ID-Based Deduplication

**Decision**: Use element IDs as the unique key for deduplication across scroll segments.

**Rationale**:
- Handles realistic cases where same element appears in multiple segments (sticky headers, navigation bars)
- Simple dict-based deduplication in O(n) time
- Aligns with existing MenuItem model's id field

**Alternatives Considered**:
- Position-based deduplication: Rejected as identical elements at different positions would be duplicated
- Content hash-based deduplication: Rejected due to potential for false negatives (similar but different elements)

### 6. Four-Parameter Element ID Generation

**Decision**: Generate unique element IDs using four parameters: `content_hash + progress + segment_index + element_index`.

**Rationale**:
- Prevents ID collisions across different scroll positions
- Content hash ensures semantic consistency
- Position parameters disambiguate identical content at different locations
- 32-character hex ID provides sufficient uniqueness

**Alternatives Considered**:
- UUID for each element: Rejected as non-deterministic—same element would get different IDs across test runs
- Two-parameter (content + position): Rejected due to collision risk with identical content at nearby positions

### 7. Scroll State Isolation by Page Key

**Decision**: Maintain separate `ScrollState` instances per page key (based on `_current_page_id`).

**Rationale**:
- Correct behavior for multi-page traversal tests
- No state pollution between different pages
- Aligns with existing base class pattern (_current_page_id)
- Simple dict-based lookup

**Alternatives Considered**:
- Global scroll state: Rejected as incorrect—different pages should have independent scroll positions
- Thread-based isolation: Rejected as unnecessary for single-threaded test execution

## Risks / Trade-offs

**[Risk] Performance degradation with large scroll segments** → Mitigation: Limit to 200 elements per list in documentation; use efficient dict-based deduplication; accumulate mode has O(n) complexity which is acceptable for test data scales

**[Risk] Element ID collision with four-parameter approach** → Mitigation: Use MD5 hash (16 chars) + position info (16 chars) = 32 char IDs; probability of collision is astronomically low for test scenarios

**[Risk] Backward compatibility issues with existing tests** → Mitigation: Subclassing ensures existing tests use base classes; new opt-in via ScrollableMock* classes; comprehensive test coverage validates non-regression

**[Risk] P0 architecture fixes not applied during implementation** → Mitigation: P0 fixes documented in PRD with code comments; test generation explicitly validates _current_page_id usage, MenuItem compatibility, coordinate/bounds format support

**[Trade-off] Accumulation mode keeps all elements in memory** → Acceptable for test data (max 200 elements); production scrolling uses different mechanisms; enables simpler deduplication and clearer test semantics

**[Trade-off] No support for nested scroll containers** → Explicitly documented as out-of-scope (V7.x); single container per page covers majority of test scenarios; nested containers add significant complexity for edge cases

## Migration Plan

This is a test-only feature with no production runtime impact. No migration needed.

**Deployment Steps**:
1. Implement `src/simulation/scroll/models.py` (ScrollSegment, ScrollState, ScrollAction)
2. Implement `ScrollableMockVisionService` extending StatefulMockVisionService
3. Implement `ScrollableMockActionExecutor` extending StatefulMockActionExecutor
4. Run generated test suite: `pytest tests/simulation/scroll/ -v`
5. Validate existing tests still pass: `pytest tests/ -v` (no regressions)

**Rollback Strategy**:
- Delete new `src/simulation/scroll/` directory
- All existing tests continue using base classes
- Zero production impact to rollback

## Open Questions

None—architecture fully specified in PRD with P0 fixes validated.
