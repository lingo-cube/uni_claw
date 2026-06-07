# Design: V6.9.2 Simulation Enhancement

## Context

### Current State

The simulation testing framework (V6.9.1) has a critical limitation: Mock services return fixed page data without state management. This causes:

1. **False positives**: Tests show COMPLETED status but exhibit incorrect behavior (e.g., AUTO_ESCAPE clicking the same button 3 times on a static page)
2. **No page transitions**: MockVisionService always returns the same page regardless of actions
3. **Weak validation**: Tests only check final status, not execution quality
4. **Incomplete traces**: Missing page transition and dynamic node lifecycle information

### Constraints

- Must maintain backward compatibility with existing MockVisionService
- Must integrate with existing trace infrastructure (SpanNode, TraceRecorder)
- Must work with existing PageAnalysis and MenuItem models (field mapping: `items` not `menu_items`, `name` not `text`)
- Must support configurable detection thresholds
- Unit tests and simulation tests must pass

### Stakeholders

- Simulation test authors need declarative fixture definitions
- V6 engine developers need enhanced trace visibility
- Quality assurance needs reliable problem detection

## Goals / Non-Goals

**Goals:**

1. Enable simulation tests to discover design/code problems through stateful mock services
2. Provide declarative YAML-based fixture and expected behavior definitions
3. Implement configurable problem detection with adjustable thresholds
4. Record complete execution traces including page transitions and dynamic node lifecycle
5. Validate actual behavior against expected behavior (actions, transitions, visitation)
6. Maintain 90%+ unit test coverage for new components

**Non-Goals:**

1. Real device testing (belongs in E2E testing)
2. Performance optimization of trace recording
3. CI/CD pipeline integration (can be added later)
4. Complex fixture features (inheritance, includes, conditionals) - deferred to future
5. Automatic migration of all existing fixtures (provide tools, manual migration)

## Decisions

### D1: Coexistence Strategy for Mock Services

**Decision**: MockVisionService and StatefulMockVisionService coexist as separate implementations.

**Rationale**: 
- Existing tests using MockVisionService should continue working without changes
- StatefulMockVisionService is a distinct implementation for stateful scenarios
- Clear separation of concerns: static vs. stateful mock behaviors
- Allows gradual migration of test fixtures

**Alternatives Considered**:
- **Enhance existing MockVisionService**: Would break existing tests, risk of regression
- **Abstract base class with two implementations**: More complex, YAGNI for current scope

### D2: PageAnalysis Field Mapping Strategy

**Decision**: StatefulMockVisionService correctly maps fixture fields to PageAnalysis model: `fixture.text` → `MenuItem.name`, `fixture.type` → `MenuItem.type enum`, `PageAnalysis.items` (not `menu_items`).

**Rationale**:
- Ensures compatibility with existing GraphEngine code
- Matches existing model definitions in `src/state/content_tree.py`
- Critical for DynamicMatcher integration (expects `text` field from items)

**Validation Requirement**: Unit tests must verify this mapping produces MenuItem objects compatible with DynamicMatcher.

### D3: YAML Format for Fixtures and Expected Behavior

**Decision**: Use YAML format for StateFixture and ExpectedBehavior definitions.

**Rationale**:
- Human-readable and declarative
- Easy to version control
- Supports hierarchical structures (pages, elements, transitions)
- Standard for test fixtures in Python projects

**Schema**:
```yaml
# StateFixture
pages:
  <page_id>:
    page_name: str
    elements: [{id, type, text, coordinate, action_target}]
    is_complete: bool
transitions:
  <trans_id>:
    trigger: element_id
    from_page: page_id
    to_page: page_id
    action: click|back|swipe

# ExpectedBehavior  
scenario: str
description: str
actions: [{action, node, target, order}]
page_transitions: [{from, to, trigger}]
visited_nodes: [str]
final_state: str
completion_mode: normal|exception|cancelled|timeout
```

### D4: Problem Detection Configuration

**Decision**: Use Pydantic BaseModel for ProblemDetectorConfig with hierarchical sensitivity levels.

**Rationale**:
- Type safety and validation
- Easy to serialize/deserialize
- Supports nested configuration
- Clear defaults with override capability

**Configuration Levels**:
1. **Code defaults**: Base thresholds in ProblemDetectorConfig.__init__
2. **Test-level**: pytest fixture providing custom config
3. **Scenario-level**: Optional override in ExpectedBehavior

### D5: Trace Model Extension Strategy

**Decision**: Add new span types as subclasses of SpanNode rather than modifying existing models.

**Rationale**:
- Backward compatibility with existing trace readers
- Clear type distinction for analysis
- Extensible without breaking changes
- Follows Open/Closed Principle

**New Span Types**:
- `PageTransitionSpan`: from_page, to_page, trigger_element, action
- `DynamicNodeLifecycleSpan`: event, node_id, parent_id, match_rule_id, element_id
- `StateDecisionSpan`: current_state, decision, reason, context

### D6: Behavior Validation Matching Strategy

**Decision**: Implement multi-level matching with confidence scoring (exact → fuzzy → none).

**Rationale**:
- Dynamic nodes may have generated IDs that don't exactly match expectations
- Fuzzy matching allows detection with appropriate warnings
- Confidence scoring enables quality assessment

**Match Priority**:
1. **Exact**: node_id or element_id exact match (confidence 1.0)
2. **Fuzzy - ID substring**: One ID contains the other (confidence 0.9)
3. **Fuzzy - Target text**: Target text match (confidence 0.7)
4. **None**: No match (confidence 0.0)

### D7: Component Integration Architecture

**Decision**: Layered architecture with clear separation of concerns.

**Layers**:
1. **Fixture Layer**: StateFixture (YAML → Python objects)
2. **Mock Layer**: StatefulMockVisionService + StatefulMockActionExecutor
3. **Execution Layer**: GraphTraversalEngine (enhanced trace recording)
4. **Validation Layer**: BehaviorValidator + ProblemDetector
5. **Test Layer**: SimulationRunner integration

**Rationale**:
- Each layer has single responsibility
- Easy to test components in isolation
- Mock layer can be swapped for real services in production

### D8: Test Organization

**Decision**: Organize tests by component and scope.

**Structure**:
```
tests/v6/
├── unit/
│   ├── test_state_fixture.py
│   ├── test_stateful_mock_vision.py
│   ├── test_expected_behavior.py
│   ├── test_behavior_validator.py
│   └── test_problem_detector.py
├── integration/
│   ├── test_simulation_e2e.py
│   └── test_bug_detection.py
└── fixtures/
    ├── simple_two_page.yaml
    ├── dynamic_buttons.yaml
    └── expected/
        └── simple_two_page_expected.yaml
```

**Rationale**:
- Clear separation of unit vs integration tests
- Fixture files co-located with tests
- Easy to run specific test suites

## Risks / Trade-offs

### R1: Field Mapping Errors

**Risk**: Incorrect mapping between fixture fields and PageAnalysis/MenuItem models.

**Mitigation**:
- Mandatory unit test `test_menu_item_compatible_with_dynamic_matcher`
- Validation test that converts PageAnalysis to DynamicMatcher input format
- Document mapping clearly in code comments

### R2: Trace Performance Impact

**Risk**: Enhanced trace recording may slow down simulation execution.

**Mitigation**:
- Trace recording only active during simulation (not production)
- Monitor performance in integration tests
- Consider configurable trace levels in future

### R3: Fixture Format Complexity

**Risk**: YAML fixture format may become complex for advanced scenarios.

**Mitigation**:
- Start with simple scenarios (2-5 pages, linear/branching)
- Document examples thoroughly
- Future expansion for inheritance/includes when needed

### R4: False Positive Problem Detection

**Risk**: Problem detector may flag normal behavior as problematic.

**Mitigation**:
- Configurable thresholds with sensible defaults
- Sensitivity levels (low/medium/high)
- Severity levels (critical/warning/info)
- Human review of warnings

### R5: State Management Bugs

**Risk**: StatefulMockVisionService may have incorrect state transitions.

**Mitigation**:
- Comprehensive unit tests for all transition scenarios
- Integration tests that verify complete round-trips
- Trace validation of page transitions

## Migration Plan

### Phase 1: Core Implementation (Days 1-4)

1. Implement StateFixture with YAML loading
2. Implement StatefulMockVisionService
3. Implement StatefulMockActionExecutor
4. Write unit tests for all components
5. Verify PageAnalysis field mapping

### Phase 2: Enhanced Trace Recording (Days 5-7)

1. Add new span types to src/trace/models.py
2. Integrate page transition recording in GraphTraversalEngine
3. Add dynamic node lifecycle tracking
4. Write integration tests for trace recording

### Phase 3: Validation & Detection (Days 8-11)

1. Implement ExpectedBehavior with YAML support
2. Implement BehaviorValidator with multi-level matching
3. Implement ProblemDetector with configurable thresholds
4. Write validation and detection tests

### Phase 4: Integration & Bug Detection (Days 12-14)

1. Update SimulationRunner to use stateful services
2. Write bug detection tests (verify original issues can be caught)
3. Create example fixtures and expected behaviors
4. Document usage patterns

### Rollback Strategy

- New components are additive, no breaking changes
- Can revert to MockVisionService if issues arise
- Fixture migration is manual and incremental

## Open Questions

1. **Q**: Should we support automatic fixture migration from virtual_pages JSON?
   - **A**: Not in initial scope. Provide migration documentation and examples first.

2. **Q**: Should ProblemDetector be configurable at runtime?
   - **A**: Yes, through ProblemDetectorConfig. Can be extended with external config later.

3. **Q**: Should trace recording be toggleable?
   - **A**: Not needed for simulation (always on). Consider for production optimization later.

4. **Q**: How to handle circular page transitions in fixtures?
   - **A**: Current scope supports cycles (A→B→A). Validation ensures target pages exist.

5. **Q**: Should ExpectedBehavior support wildcards or patterns?
   - **A**: Not in initial scope. Exact matching with fuzzy fallback handles most cases.
