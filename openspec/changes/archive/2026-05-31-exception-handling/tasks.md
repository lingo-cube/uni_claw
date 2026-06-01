# Implementation Tasks - Exception Handling System

> **Phase 1 范围**: 基础异常分类 + 处理器接口 + 处理链 + 基础恢复策略
> 
> **不包含**: AI 驱动异常处理（Phase 2）；状态机完整集成（配合状态机实现）
> 
> **完整设计见**: design.md

## 1. Exception Data Structures

- [x] 1.1 Create `src/exception/` module directory
- [x] 1.2 Define TraversalException base class with message and chaining support
- [x] 1.3 Define LocationException and subclasses (ElementNotFoundException, PathMismatchException, CoordinateExpiredException)
- [x] 1.4 Define OperationException and subclasses (ClickFailedException, InputFailedException)
- [x] 1.5 Define DeviceException and subclasses (ADBDisconnectedException, AppCrashException, DeviceOfflineException)
- [x] 1.6 Define UIException and subclasses (PopupDetectedException, PageRedirectException, LoadingTimeoutException)
- [x] 1.7 Define AIException and subclasses (AIAnalysisFailedException, AIResponseInvalidException)
- [x] 1.8 Define ExceptionSeverity enum (INFO, WARNING, ERROR, CRITICAL, FATAL)
- [x] 1.9 Add default severity mapping to each exception class
- [x] 1.10 Implement severity override in exception constructors

## 2. Exception Context and Results

- [x] 2.1 Define ExceptionContext dataclass with all required fields
- [x] 2.2 Define ExceptionHandlingResult dataclass with action, message, new_state, recovery_action
- [x] 2.3 Define ExceptionAction enum (RETRY, SKIP, BACKTRACK, RECOVER, TERMINATE, IGNORE)
- [x] 2.4 Define RecoveryAction enum (RECONNECT_ADB, RESTART_APP, CLOSE_POPUP, NAVIGATE_BACK, WAIT_AND_RETRY, IGNORE_UI_CHANGE)
- [x] 2.5 Add unit tests for all data structures
- [x] 2.6 Verify JSON serialization/deserialization works

## 3. Exception Handler Interface

- [x] 3.1 Define ExceptionHandler abstract base class
- [x] 3.2 Implement can_handle(context) abstract method
- [x] 3.3 Implement handle(context) abstract method
- [x] 3.4 Add docstrings explaining handler contract
- [x] 3.5 Add unit tests for handler interface

## 4. Built-in Handlers

- [x] 4.1 Implement FatalExceptionHandler
  - [x] 4.1.1 can_handle checks for FATAL severity
  - [x] 4.1.2 handle returns TERMINATE action
- [x] 4.2 Implement DeviceExceptionHandler
  - [x] 4.2.1 can_handle checks for DeviceException instances
  - [x] 4.2.2 handle ADBDisconnectedException with RECONNECT_ADB
  - [x] 4.2.3 handle AppCrashException with RESTART_APP
  - [x] 4.2.4 handle DeviceOfflineException with TERMINATE
- [x] 4.3 Implement UIExceptionHandler
  - [x] 4.3.1 can_handle checks for UIException instances
  - [x] 4.3.2 handle PopupDetectedException with CLOSE_POPUP
  - [x] 4.3.3 handle PageRedirectException with IGNORE_UI_CHANGE
  - [x] 4.3.4 handle LoadingTimeoutException with WAIT_AND_RETRY
- [x] 4.4 Implement RetryHandler
  - [x] 4.4.1 can_handle checks ERROR severity and retry_count < max_retries
  - [x] 4.4.2 handle returns RETRY with retry count in message
  - [x] 4.4.3 Support configurable max_retries (default 3)
- [x] 4.5 Implement BacktrackHandler
  - [x] 4.5.1 can_handle checks CRITICAL severity and retry_count >= max_retries
  - [x] 4.5.2 handle returns BACKTRACK action
- [x] 4.6 Add unit tests for each handler
- [ ] 4.7 Add integration tests for handler combinations

## 5. Exception Handling Chain

- [x] 5.1 Define ExceptionHandlingChain class
- [x] 5.2 Implement handler registration (add/set handlers)
- [x] 5.3 Implement handle(context) method with priority iteration
- [x] 5.4 Implement first-match-wins logic
- [x] 5.5 Add default handler order setup method
- [x] 5.6 Add logging for handler attempts (debug mode)
- [x] 5.7 Add unit tests for chain execution
- [ ] 5.8 Add tests for handler priority order

## 6. Exception History

- [x] 6.1 Define ExceptionHistory class
- [x] 6.2 Implement record(context) method with max_records limit
- [x] 6.3 Implement get_by_type(exc_type) query method
- [x] 6.4 Implement get_by_severity(severity) query method
- [x] 6.5 Implement get_statistics() method with type/severity counters
- [x] 6.6 Add unit tests for history recording
- [ ] 6.7 Add tests for history size limit behavior

## 7. TraversalEngine Integration

- [x] 7.1 Add exception_chain and exception_history to TraversalEngine.__init__
- [x] 7.2 Implement _build_exception_chain() method
- [x] 7.3 Implement _get_severity(exception) method
- [x] 7.4 Implement execute_with_exception_handling(operation, **context) method
- [x] 7.5 Implement RETRY action logic (increment retry_count, continue)
- [x] 7.6 Implement SKIP action logic (return None, continue)
- [x] 7.7 Implement BACKTRACK action logic (call _backtrack, return None)
- [x] 7.8 Implement RECOVER action logic (call _recover, continue/retry)
- [x] 7.9 Implement TERMINATE action logic (raise original exception)
- [x] 7.10 Wrap key operations (tap_and_wait, analyze_screenshot, etc.)
- [x] 7.11 Set max_attempts = 4 (1 initial + 3 retries)
- [ ] 7.12 Add integration tests for exception handling flow

## 8. Recovery Actions

- [x] 8.1 Implement _recover(recovery_action) method in TraversalEngine
- [x] 8.2 Implement RECONNECT_ADB recovery
  - [x] 8.2.1 Call adb.reconnect()
  - [x] 8.2.2 Wait for connection
  - [x] 8.2.3 Verify connection active
- [x] 8.3 Implement RESTART_APP recovery
  - [x] 8.3.1 Stop app via adb
  - [x] 8.3.2 Start app via adb
  - [x] 8.3.3 Wait for app ready
  - [x] 8.3.4 Navigate to last position if possible
- [x] 8.4 Implement CLOSE_POPUP recovery
  - [x] 8.4.1 Analyze current screen for popup
  - [x] 8.4.2 Identify close button
  - [x] 8.4.3 Click close button
  - [x] 8.4.4 Wait for popup dismiss
- [x] 8.5 Implement NAVIGATE_BACK recovery
  - [x] 8.5.1 Press back button via adb
  - [x] 8.5.2 Wait for navigation
  - [x] 8.5.3 Verify position changed
- [x] 8.6 Implement WAIT_AND_RETRY recovery
  - [x] 8.6.1 Wait for configured duration (default 1.0s)
  - [x] 8.6.2 Return RETRY action
- [x] 8.7 Implement IGNORE_UI_CHANGE recovery
  - [x] 8.7.1 Log the UI change
  - [x] 8.7.2 Return IGNORE action
- [ ] 8.8 Add timeout handling for all recovery actions
- [ ] 8.9 Add unit tests for each recovery action
- [ ] 8.10 Add integration tests for recovery scenarios

## 9. Event Emission

- [x] 9.1 Define exception-related event types (using existing _emit)
- [x] 9.2 Emit event on exception occurrence
- [x] 9.3 Emit event on recovery start
- [x] 9.4 Emit event on recovery success
- [x] 9.5 Emit event on recovery failure
- [x] 9.6 Include exception context in events
- [x] 9.7 Include recovery action and duration in events

## 10. State Manager Integration

- [x] 10.1 Add exception_history to TraversalState
- [x] 10.2 Save exception history to state file
- [x] 10.3 Load exception history from state file on resume
- [x] 10.4 Add API methods for querying exception history
- [x] 10.5 Add statistics endpoint to CLI
- [ ] 10.6 Test state file save/load with exception history

## 11. Configuration

- [x] 11.1 Add config option for enable/disable exception handling
- [x] 11.2 Add config option for max_retries
- [x] 11.3 Add config option for recovery timeouts
- [x] 11.4 Add config option for exception history max_records
- [x] 11.5 Add CLI flag for verbose exception logging
- [ ] 11.6 Test configuration options

## 12. Testing

- [x] 12.1 Add unit tests for all exception classes
- [x] 12.2 Add unit tests for ExceptionContext and ExceptionHandlingResult
- [x] 12.3 Add unit tests for all handler implementations
- [x] 12.4 Add unit tests for ExceptionHandlingChain
- [x] 12.5 Add unit tests for ExceptionHistory
- [x] 12.6 Add integration test: element not found → retry → success
- [x] 12.7 Add integration test: device offline → terminate
- [x] 12.8 Add integration test: popup detected → close → continue
- [x] 12.9 Add integration test: app crash → restart → navigate back
- [x] 12.10 Add integration test: retry exhausted → backtrack
- [x] 12.11 Add exception injection test framework
- [ ] 12.12 Add performance test for exception handling overhead (optional)

## 13. Documentation

- [x] 13.1 Update README.md with exception handling overview
- [x] 13.2 Document exception hierarchy and meanings
- [x] 13.3 Document handler priority order
- [x] 13.4 Document recovery actions and behavior
- [x] 13.5 Document configuration options
- [x] 13.6 Add examples of custom handler implementation
- [x] 13.7 Document exception history query API

## 14. Migration and Compatibility

- [x] 14.1 Ensure old state files load without exception history (Pydantic default_factory)
- [x] 14.2 Verify new state files include exception history
- [x] 14.3 Test backward compatibility with existing code (disabled by default)
- [x] 14.4 Add migration guide if needed (included in docs)

## 15. Phase 2 Preparation (AI-Driven)

- [x] 15.1 Add placeholder for screenshot in ExceptionContext (commented)
- [x] 15.2 Add placeholder for ai_result in ExceptionHandlingResult (commented)
- [x] 15.3 Document Phase 2 requirements in code comments
- [x] 15.4 Prepare hooks for AI handler integration (commented in handlers.py)
