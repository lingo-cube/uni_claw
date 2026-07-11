## 1. HandlePreconditionCheck — Assume Pass + Trace Logging

- [x] 1.1 Replace HandlePreconditionCheck stub (always→Execute) with implemented version that calls TraceCoordinator.RecordDecision("precondition_assume_pass") before returning Execute
- [x] 1.2 Write HandlePreconditionCheck test: precondition assume pass → Execute transition + trace decision recorded
- [x] 1.3 Verify all existing tests still pass after HandlePreconditionCheck change

## 2. HandleResultVerify — 3-Round Retry + Popup Detection

- [x] 2.1 Replace HandleResultVerify stub (always→Branch) with implemented version that checks PageSnapshotManager.HasChanged
- [x] 2.2 Implement retry loop: up to 3 rounds of IVisionProvider.GetPageAnalysis() + HasChanged re-check when no change detected
- [x] 2.3 Add PopupDetector check inside retry loop: if popup detected, transition to PopupHandling instead of continuing retry
- [x] 2.4 Add TraceCoordinator.RecordDecision for each retry round and final outcome
- [x] 2.5 Write HandleResultVerify tests: (a) first-check pass → Branch, (b) retry succeeds round 2 → Branch, (c) 3 rounds fail → Branch, (d) popup detected round 1 → PopupHandling, (e) popup detected round 2 → PopupHandling, (f) trace decisions recorded
- [x] 2.6 Verify all existing tests still pass

## 3. HandleErrorHandling — 5-Strategy RecoveryExecutor Delegation

- [x] 3.1 Replace HandleErrorHandling stub (always→NodeSelect) with implemented version that delegates to ErrorClassifier → ErrorStrategySelector → RecoveryExecutor.Execute
- [x] 3.2 Implement FSM transition mapping: Retry→Execute, Backtrack→NodeSelect, Skip→Branch, Continue→NodeSelect, Abort→FrameComplete
- [x] 3.3 Implement consecutive error tracking: increment on Retry, reset on non-Retry outcome
- [x] 3.4 Add TraceCoordinator.RecordStateDecision with selected ErrorStrategy + FSM target
- [x] 3.5 Add TraceCoordinator.RecordErrorSpan with error details (ErrorType, ErrorMessage, Severity)
- [x] 3.6 Write HandleErrorHandling tests: (a) Retry → Execute + consecutive increment, (b) Backtrack → NodeSelect + consecutive reset, (c) Skip → Branch, (d) Continue → NodeSelect, (e) Abort → FrameComplete, (f) RecoveryExecutor fallback → Abort, (g) trace recorded on each strategy
- [x] 3.7 Verify all existing tests still pass

## 4. HandlePopupHandling — PopupHandler Pipeline Delegation

- [x] 4.1 Replace HandlePopupHandling stub (always→ResultVerify) with implemented version that delegates to PopupHandler.HandlePopup()
- [x] 4.2 Implement FSM transition mapping: Success=true → ResultVerify, Success=false → ErrorHandling
- [x] 4.3 Add TraceCoordinator.RecordStateTransition with FsmType="TraversalFSM" + dismiss outcome
- [x] 4.4 Add TraceCoordinator.RecordDecision with dismiss outcome details
- [x] 4.5 Write HandlePopupHandling tests: (a) PopupHandler returns Success=true → ResultVerify, (b) PopupHandler returns Success=false → ErrorHandling, (c) PopupClassifier Permission popup → dismiss strategy, (d) trace transitions recorded
- [x] 4.6 Verify all existing tests still pass

## 5. Verification — Full FSM Integration

- [x] 5.1 Run full test suite: all tests pass (603 tests)
- [x] 5.2 Write FSM integration test: complete traversal cycle through all 8 states with all handlers implemented
- [x] 5.3 Verify TraversalResult.Trace populated correctly for all handler transitions
- [x] 5.4 Confirm no HandleFrameComplete scope change needed (design D5 — minimal implementation is correct, stack pop in StepOrchestrator)
