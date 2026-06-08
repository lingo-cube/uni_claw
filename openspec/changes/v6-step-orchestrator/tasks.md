## 1. TraceCoordinator extraction

- [ ] 1.1 Create `src/traversal/trace_coordinator.py` — extract all 15 `_record_*` methods from Engine into a single class holding `trace_recorder`
- [ ] 1.2 Wire TraceCoordinator into Engine.__init__, replace all `self._record_*` calls with `self._trace.record_*`
- [ ] 1.3 Replace `record_lifecycle` + `record_skip` callbacks in DynamicChildManager with `TraceCoordinator` reference
- [ ] 1.4 Replace `trace_recorder` + `should_record` in EntryPolicyExecutor with `TraceCoordinator` reference
- [ ] 1.5 Delete old `_record_*` methods from Engine
- [ ] 1.6 Run simulation + full test suite — verify 138 steps, 79/79 tests

## 2. StepOrchestrator extraction

- [ ] 2.1 Create `StepContext` dataclass in `src/traversal/step_orchestrator.py` — bundle all 10+ dependencies
- [ ] 2.2 Extract `_step_once` into `StepOrchestrator.execute_step(ctx)` using StepContext
- [ ] 2.3 Wire StepOrchestrator into Engine.run() main loop — create StepContext once, call per iteration
- [ ] 2.4 Run simulation + full test suite — verify 138 steps, 79/79 tests

## 3. Engine cleanup

- [ ] 3.1 Remove extracted methods from Engine — keep only `__init__`, `initialize`, `run`, `_should_continue`, `_check_completion_policy`, `_create_result`, `_NodeStackAdapter`
- [ ] 3.2 Verify Engine is under 900 lines
- [ ] 3.3 Run full V6 test suite — all tests pass
