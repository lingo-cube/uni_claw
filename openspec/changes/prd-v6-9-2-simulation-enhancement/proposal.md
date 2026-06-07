# Proposal: V6.9.2 Simulation Enhancement

## Why

After implementing V6.9.1 dynamic matching, simulation testing revealed a critical issue: tests can "pass" with COMPLETED status while exhibiting completely incorrect behavior. The core problem is that Mock services lack state management capabilities, cannot simulate page transitions, and validation logic is too permissive. This enhancement aims to make simulation tests actually discover design and code problems rather than just passing.

## What Changes

### New Components

- **StateFixture**: YAML-based fixture format for defining page states and transition rules
- **StatefulMockVisionService**: Mock vision service with state management and page transition simulation
- **StatefulMockActionExecutor**: Mock action executor that coordinates with stateful vision service
- **ExpectedBehavior**: YAML format for defining expected test behavior
- **BehaviorValidator**: Validates actual execution against expected behavior (action sequences, page transitions, node visitation)
- **ProblemDetector**: Auto-detects abnormal execution patterns (infinite loops, repeated actions, unvisited nodes, orphaned dynamic nodes)

### Enhanced Trace Recording

- **PageTransitionSpan**: Records page transitions with from/to pages and trigger elements
- **DynamicNodeLifecycleSpan**: Tracks dynamic node lifecycle events (created, matched, pushed, executed, popped)
- **StateDecisionSpan**: Records state machine decision points with reasoning

### Configuration & Thresholds

- **ProblemDetectorConfig**: Configurable detection thresholds for max action repeats, loop depth, and feature toggles
- **Flexible sensitivity levels**: low/medium/high for loop detection

### Backward Compatibility

- MockVisionService remains for static page scenarios
- StatefulMockVisionService is a new implementation for stateful scenarios
- Migration tools to convert existing virtual_pages JSON to StateFixture YAML

## Capabilities

### New Capabilities

- **state-fixture**: YAML-based page state and transition rule definitions
- **stateful-mock-services**: Mock services with state management and page transition simulation
- **behavior-validation**: Expected behavior definition and validation framework
- **problem-detection**: Automatic detection of abnormal execution patterns
- **enhanced-trace-recording**: Extended trace models for page transitions and dynamic node lifecycle

### Modified Capabilities

- **simulation-runner**: Enhanced to support stateful mock services and enhanced trace recording

## Impact

### Code Changes

- `src/simulation/`: New modules for state_fixture, stateful_mock_vision, stateful_mock_action, expected_behavior, behavior_validator, problem_detector
- `src/trace/models.py`: Extended with new span types (PageTransitionSpan, DynamicNodeLifecycleSpan, StateDecisionSpan)
- `src/traversal/graph_engine.py`: Enhanced to record page transitions and dynamic node lifecycle events
- `src/simulation/runner.py`: Updated to integrate stateful services

### Test Changes

- New unit tests for each new component
- New integration tests for end-to-end simulation scenarios
- New bug detection tests that verify the original issues can be caught
- Migration of existing test fixtures to StateFixture YAML format

### Dependencies

- Pydantic: For configuration models (ProblemDetectorConfig)
- YAML parsing: For fixture and expected behavior files
- Existing trace models and infrastructure

### Breaking Changes

None - new components are additive, existing MockVisionService remains functional

## Success Criteria

1. Simulation tests can detect the original AUTO_ESCAPE infinite loop bug
2. Simulation tests can detect repeated action patterns on static pages
3. Behavior validation catches mismatches between expected and actual execution
4. Problem detector identifies abnormal patterns with configurable thresholds
5. Unit test coverage > 90% for new components
6. Page transitions and dynamic node lifecycle are properly recorded in traces
