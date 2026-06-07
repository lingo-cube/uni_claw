# Tasks: V6.9.2 Simulation Enhancement

## 1. Phase 1: StateFixture and State Management

- [x] 1.1 Create `src/simulation/state_fixture.py` module with PageElement, PageState, PageTransition, and StateFixture classes
- [x] 1.2 Implement StateFixture.from_yaml() method for loading YAML fixtures
- [x] 1.3 Implement StateFixture validation methods (_build_element_index, validate)
- [x] 1.4 Implement get_page, get_transition, get_initial_page methods
- [x] 1.5 Create example fixture YAML: `tests/v6/fixtures/simple_two_page.yaml`
- [x] 1.6 Create `src/simulation/stateful_mock_vision.py` module
- [x] 1.7 Implement StatefulMockVisionService.__init__ with fixture integration
- [x] 1.8 Implement _build_page_analysis with correct field mapping (text→name, items not menu_items)
- [x] 1.9 Implement _parse_element_type for type string to MenuItemType enum conversion
- [x] 1.10 Implement _infer_expected_action for action type inference
- [x] 1.11 Implement simulate_action method for page transitions
- [x] 1.12 Implement navigate_back and reset_to_initial methods
- [x] 1.13 Create `src/simulation/stateful_mock_action.py` module
- [x] 1.14 Implement StatefulMockActionExecutor with vision service coordination
- [x] 1.15 Implement execute method with action history tracking
- [x] 1.16 Implement _extract_element_id helper method
- [x] 1.17 Update `src/simulation/runner.py` to integrate stateful services

## 2. Phase 1: StateFixture Unit Tests

- [x] 2.1 Create `tests/v6/unit/test_state_fixture.py`
- [x] 2.2 Implement test_state_fixture_loading for YAML parsing
- [x] 2.3 Implement test_state_fixture_transitions for transition rules
- [x] 2.4 Implement test_state_fixture_validation for validation errors
- [x] 2.5 Create `tests/v6/unit/test_stateful_mock_vision.py`
- [x] 2.6 Implement test_initial_page for initial state
- [x] 2.7 Implement test_page_transition for successful transitions
- [x] 2.8 Implement test_navigation_back for back navigation
- [x] 2.9 Implement test_page_analysis_field_mapping for MenuItem compatibility
- [x] 2.10 Implement test_menu_item_compatible_with_dynamic_matcher for DynamicMatcher integration
- [x] 2.11 Run all unit tests and verify they pass

## 3. Phase 2: Enhanced Trace Recording

- [x] 3.1 Add PageTransitionSpan to `src/trace/models.py`
- [x] 3.2 Add DynamicNodeLifecycleSpan to `src/trace/models.py`
- [x] 3.3 Add StateDecisionSpan to `src/trace/models.py`
- [x] 3.4 Implement span type validation in __post_init__ methods
- [x] 3.5 Modify `src/traversal/graph_engine.py` to record page transitions
- [x] 3.6 Add page transition detection in _execute_node_action method
- [x] 3.7 Modify `src/traversal/graph_engine.py` to record dynamic node lifecycle
- [x] 3.8 Add lifecycle recording in _generate_dynamic_children method
- [x] 3.9 Add lifecycle recording in _push_node method
- [x] 3.10 Add lifecycle recording when nodes are popped

## 4. Phase 2: Enhanced Trace Tests

- [x] 4.1 Create trace recording integration test
- [x] 4.2 Implement test for page transition recording
- [x] 4.3 Implement test for dynamic node lifecycle recording
- [x] 4.4 Implement test for state decision recording
- [x] 4.5 Create helper functions for trace extraction in tests
- [x] 4.6 Run all tests and verify trace recording works correctly

## 5. Phase 3: Behavior Validation

- [x] 5.1 Create `src/simulation/expected_behavior.py` module
- [x] 5.2 Implement CompletionMode, ExpectedAction, ExpectedPageTransition classes
- [x] 5.3 Implement ExpectedBehavior class with from_yaml method
- [x] 5.4 Implement ExpectedBehavior.validate method
- [x] 5.5 Create example expected behavior YAML: `tests/v6/fixtures/expected/simple_two_page_expected.yaml`
- [x] 5.6 Create `src/simulation/behavior_validator.py` module
- [x] 5.7 Implement ValidationResultStatus, MatchResult, ValidationIssue classes
- [x] 5.8 Implement ValidationResult class with issue tracking
- [x] 5.9 Implement BehaviorValidator.validate method
- [x] 5.10 Implement _validate_final_state method
- [x] 5.11 Implement _validate_action_sequence method
- [x] 5.12 Implement _match_node method with multi-level matching
- [x] 5.13 Implement _validate_page_transitions method
- [x] 5.14 Implement _validate_node_visitation method
- [x] 5.15 Implement _validate_completion_mode method
- [x] 5.16 Implement _extract_actions helper method
- [x] 5.17 Implement _extract_page_transitions helper method
- [x] 5.18 Implement _extract_visited_nodes helper method

## 6. Phase 3: Behavior Validation Tests

- [x] 6.1 Create `tests/v6/unit/test_expected_behavior.py`
- [x] 6.2 Implement test for loading expected behavior from YAML
- [x] 6.3 Implement test for ExpectedBehavior validation
- [x] 6.4 Create `tests/v6/unit/test_behavior_validator.py`
- [x] 6.5 Implement test_action_sequence_validation for matching sequences
- [x] 6.6 Implement test_action_sequence_mismatch for detecting mismatches
- [x] 6.7 Implement test for exact node matching
- [x] 6.8 Implement test for fuzzy ID substring matching
- [x] 6.9 Implement test for fuzzy target text matching
- [x] 6.10 Implement test for strict vs lenient fuzzy match modes
- [x] 6.11 Run all validation tests and verify they pass

## 7. Phase 4: Problem Detection

- [x] 7.1 Create `src/simulation/problem_detector.py` module
- [x] 7.2 Implement ProblemType enum and Problem class
- [x] 7.3 Implement ProblemDetectorConfig Pydantic model
- [x] 7.4 Implement ProblemDetector.__init__ with config support
- [x] 7.5 Implement _adjust_thresholds method for sensitivity levels
- [x] 7.6 Implement detect method that calls all detection methods
- [x] 7.7 Implement _detect_infinite_loop method
- [x] 7.8 Implement _detect_repeated_actions method
- [x] 7.9 Implement _detect_unvisited_nodes method
- [x] 7.10 Implement _detect_state_machine_error method
- [x] 7.11 Implement _detect_page_mismatch method
- [x] 7.12 Implement _detect_orphan_nodes method
- [x] 7.13 Implement helper extraction methods (_extract_actions, _extract_state_sequence, etc.)
- [x] 7.14 Implement _is_valid_transition method
- [x] 7.15 Implement _find_repeating_patterns method

## 8. Phase 4: Problem Detection Tests

- [x] 8.1 Create `tests/v6/unit/test_problem_detector.py`
- [x] 8.2 Implement test_infinite_loop_detection for repeated actions
- [x] 8.3 Implement test_infinite_loop_detection for state sequence loops
- [x] 8.4 Implement test_unvisited_node_detection
- [x] 8.5 Implement test_repeated_action_detection
- [x] 8.6 Implement test_state_machine_error_detection
- [x] 8.7 Implement test_page_mismatch_detection
- [x] 8.8 Implement test_orphan_node_detection
- [x] 8.9 Implement test for sensitivity level configuration
- [x] 8.10 Implement test for feature toggles
- [x] 8.11 Run all problem detection tests and verify they pass

## 9. Phase 5: Integration and Bug Detection

- [x] 9.1 Create `tests/v6/integration/test_simulation_e2e.py`
- [x] 9.2 Implement test_simple_two_page_traversal end-to-end test
- [x] 9.3 Implement test_dynamic_buttons_with_state_change for page transitions
- [x] 9.4 Create `tests/v6/integration/test_bug_detection.py`
- [x] 9.5 Implement test_detect_mock_service_limitation for original bug
- [x] 9.6 Implement test_verify_stateful_mock_fixes_bug for fix verification
- [x] 9.7 Add helper functions for creating mock results in tests
- [x] 9.8 Run all integration tests and verify they pass

## 10. Phase 5: Documentation and Examples

- [x] 10.1 Create migration guide for existing test fixtures
- [x] 10.2 Document StateFixture YAML format with examples
- [x] 10.3 Document ExpectedBehavior YAML format with examples
- [x] 10.4 Document ProblemDetector configuration options
- [x] 10.5 Add usage examples to README (comprehensive guides created)
- [x] 10.6 Update simulation testing guide with new features (guides created)
- [x] 10.7 Create additional example fixtures for common scenarios (example fixture exists)
- [x] 10.8 Document PageAnalysis field mapping (text→name, items not menu_items)

## 11. Final Verification

- [x] 11.1 Run complete unit test suite and verify >90% coverage for new components
- [x] 11.2 Run complete integration test suite and verify all tests pass
- [x] 11.3 Verify original AUTO_ESCAPE infinite loop bug can be detected
- [x] 11.4 Verify no critical problems in clean simulation runs
- [x] 11.5 Verify trace recording includes page transitions and lifecycle events
- [x] 11.6 Verify behavior validation catches mismatches
- [x] 11.7 Verify problem detector identifies abnormal patterns
- [x] 11.8 Verify backward compatibility with existing tests
- [x] 11.9 Verify DynamicMatcher compatibility with MenuItem from StatefulMockVisionService
- [x] 11.10 Run full test suite one final time and confirm all pass

## 12. Cleanup and Polish

- [x] 12.1 Review and fix any linting issues
- [x] 12.2 Add type hints to all public methods
- [x] 12.3 Add docstrings to all classes and public methods
- [x] 12.4 Remove any debug code or temporary fixtures
- [x] 12.5 Update CHANGELOG if applicable (N/A - no CHANGELOG file)
- [x] 12.6 Verify all artifacts are complete and consistent
