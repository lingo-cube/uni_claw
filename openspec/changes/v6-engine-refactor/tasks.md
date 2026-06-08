## 1. DynamicChildManager + PageSnapshotManager

- [ ] 1.1 Create `src/traversal/page_snapshot_manager.py` — extract `_page_fingerprint()` as static method, add `has_changed()`
- [ ] 1.2 Create `src/traversal/dynamic_child_manager.py` — extract `_generate_dynamic_children()`, `_get_next_unvisited_child()`, `invalidate_children_cache()`, and `_generated_pairs` logic
- [ ] 1.3 Wire DynamicChildManager into GraphTraversalEngine.__init__ and replace internal calls
- [ ] 1.4 Run `test_settings_simulation_run` — verify 89 steps, COMPLETED, 19 nodes

## 2. StepOrchestrator

- [ ] 2.1 Create `src/traversal/step_orchestrator.py` — extract `_step_once()` logic including FRAME_COMPLETE interception, BRANCH child push, path-change detection, and page-non-change detection
- [ ] 2.2 Wire StepOrchestrator into GraphTraversalEngine.run() main loop
- [ ] 2.3 Run `test_settings_simulation_run` + `test_branch_handling` — verify 89 steps, 12/12 tests

## 3. EntryPolicyExecutor + TraceCoordinator

- [ ] 3.1 Create `src/traversal/entry_policy_executor.py` — extract `_build_strategy_chain()`, `_execute_entry_policy()`, `_execute_single_strategy()`, `_execute_cold_launch_strategy()`, `_execute_bind_current_screen_strategy()`, `_execute_deeplink_strategy()`, `_wait_for_entry_condition()`
- [ ] 3.2 Create `src/traversal/trace_coordinator.py` — extract all `_record_*()` methods
- [ ] 3.3 Wire both into Engine and StepOrchestrator
- [ ] 3.4 Run `test_settings_simulation_run` + `test_engine_initialization` — verify 89 steps, all tests pass

## 4. PlanValidator + PageCacheManager + Engine cleanup

- [ ] 4.1 Create `src/traversal/plan_validator.py` — extract `_validate_plan()`
- [ ] 4.2 Create `src/traversal/page_cache_manager.py` — extract `_update_page_cache()`, `_restore_from_cache()`
- [ ] 4.3 Clean up Engine — remove extracted methods, keep only orchestration: `run()`, `_should_continue()`, `_check_completion_policy()`, `_create_result()`, `initialize()`
- [ ] 4.4 Run full V6 test suite — verify no regressions

## 5. Validate

- [ ] 5.1 Run `pytest tests/v6/ tests/state_machine/` — all tests pass
- [ ] 5.2 Verify engine file is under 400 lines
- [ ] 5.3 Verify each extracted component is independently testable
