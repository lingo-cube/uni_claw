# Phase 2.2 Refactoring Design — Guard Tests + Handler Wrapper + Trace Minimal

> Date: 2026-07-10
> Status: Approved
> Scope: 3 subsystems, sequential implementation (Guard → Handler → Trace)

## Overview

This change covers 3 subsystems in priority order:

1. **Guard tests** (C-3, C-4, C-9, C-10) — CI-enforced architecture constraints
2. **Handler wrapper** (D-16, Option B) — ContainerHandler + ErrorHandler unified pipeline entry
3. **Trace minimal** — SpanType enum + PageTransition record, unlocking D-E4 TODO dimensions

Each subsystem is independent; they are implemented sequentially because Handler wrapper benefits from Guard tests already being in place (C-9 verifies sealed record class), and Trace minimal benefits from Handler wrapper providing pipeline-level trace hook points for future SpanType annotation.

---

## Section 1: Guard Tests (C-3, C-4, C-9, C-10)

### Current state

`ArchitectureGuardTests.cs` has 12 enum guard + 6 dependency guard tests. Missing 4 constitution constraint guards:

| Constraint | Missing | Target |
|------------|---------|--------|
| C-3 | Domain 三岛零互 import namespace isolation | Phase 2.2 |
| C-4 | FSM 独立性 type dependency check | Phase 2.2 |
| C-9 | sealed record class convention | Phase 2.2/3 |
| C-10 | DomainValidationException unified validation | Phase 2.2 |

### Design

Implementation follows existing `FindSourceRoot()` + file scan pattern.

#### C-3: Domain_Subdomains_ZeroCrossImport

Scan all `.cs` files under `Domain/` subdirectories:

- `Domain/Vision/` files must NOT `using UniClaw.Core.Domain.Content` or `UniClaw.Core.Domain.Common`
- `Domain/Content/` files must NOT `using UniClaw.Core.Domain.Vision` or `UniClaw.Core.Domain.Common`
- `Domain/Common/` files must NOT `using UniClaw.Core.Domain.Domain.Vision` or `UniClaw.Core.Domain.Content`
- Exception: `Domain/Mappings/` (the bridge) CAN reference Vision and Content

#### C-4: FSMs_DoNotShareTypes

Check `TraversalFSM.cs` and `GlobalFSM.cs`:

- TraversalFSM must NOT reference GlobalFSM-specific types (GlobalState, GlobalTransition etc.)
- GlobalFSM must NOT reference TraversalFSM-specific types (TraversalState, TraversalTransition etc.)
- Exception: both CAN reference `ITraversalContext` (coordination interface, not FSM type)
- Note: D-7 records the deviation that GlobalState setter on ITraversalContext creates a type-level cross-FSM dependency. Guard test does NOT validate ITraversalContext content (that's Phase 3 per D-7).

#### C-9: AllRecords_AreSealedRecordClass

Scan all `.cs` files under `Domain/`, `StateMachine/`, `Traversal/`, `Graph/`:

- Match `record class` definitions
- Assert each has `sealed` keyword preceding `record class`
- Exception: `TraversalRuntimeContext` is `sealed class` (not record — 26 mutable fields)

#### C-10: Domain_UsesDomainValidationException

Scan all `.cs` files under `Domain/`:

- Assert NO `throw new InvalidOperationException` or `throw new ArgumentException`
- ValueError is a Python type — no C# equivalent to check
- Note: `Domain.Mappings/ElementTypeMapper` uses graceful fallback (IsValid notification), not throw — this is correct

### Test organization

```csharp
// In ArchitectureGuardTests.cs
public class NamespaceIsolationGuardTests  // C-3, C-4
{
    [Fact] Domain_Subdomains_ZeroCrossImport()      // C-3
    [Fact] FSMs_DoNotShareTypes()                    // C-4
}

public class CodingConventionGuardTests             // C-9, C-10
{
    [Fact] AllRecords_AreSealedRecordClass()         // C-9
    [Fact] Domain_UsesDomainValidationException()    // C-10
}
```

### Constitution updates

| Constraint | Guard field update |
|------------|-------------------|
| C-3 | `NamespaceIsolationGuardTests.Domain_Subdomains_ZeroCrossImport` |
| C-4 | `NamespaceIsolationGuardTests.FSMs_DoNotShareTypes` |
| C-9 | `CodingConventionGuardTests.AllRecords_AreSealedRecordClass` |
| C-10 | `CodingConventionGuardTests.Domain_UsesDomainValidationException` |

---

## Section 2: Handler Wrapper (D-16, Option B)

### Current state

PopupHandler has unified entry `HandlePopup()` (6-step pipeline + top-level try/catch). Container and Error are 3 independent sub-components with no unified entry, no pipeline-level fallback.

No production code (TraversalEngine, StepOrchestrator, Simulation) calls the sub-components. TraversalFSM `HandleErrorHandling()` and `HandleFrameComplete()` are placeholders.

### Design

#### ContainerHandler wrapper

```csharp
public sealed class ContainerHandler
{
    private readonly CompletionDetector _detector;
    private readonly FallbackDecider _decider;
    private readonly ContainerActionExecutor _executor;

    public ContainerHandler(
        CompletionDetector? detector = null,
        FallbackDecider? decider = null,
        ContainerActionExecutor? executor = null)
    {
        _detector  = detector  ?? new CompletionDetector();
        _decider   = decider   ?? new FallbackDecider();
        _executor  = executor  ?? new ContainerActionExecutor();
    }

    /// <summary>
    /// HandleContainer — 3-step pipeline: detect → decide → execute.
    /// Pipeline-level try/catch fallback → ContainerActionResult(Back, false, ...).
    /// </summary>
    public ContainerActionResult HandleContainer(
        CompletionContext completionCtx,
        bool canContinue,
        string nodeId,
        ITraversalContext traversalContext)
    {
        try
        {
            // Step 1: Detect completion
            var completion = _detector.DetectCompletion(completionCtx);

            // Step 2: Decide fallback action
            var fallback = _decider.DecideFallback(completion, canContinue);

            // Step 3: Execute fallback action
            var containerCtx = new ContainerContext(
                nodeId, completionCtx.CurrentDepth, traversalContext);
            return _executor.Execute(fallback, containerCtx);
        }
        catch (Exception ex)
        {
            return new ContainerActionResult(
                FallbackAction.Back, false,
                $"Unhandled exception during container handling: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
```

**Signature rationale**: CompletionContext lacks NodeId and ITraversalContext (it only has 7 numerical fields). ContainerContext requires these. Separate parameters bridge the gap. `canContinue` is caller-provided knowledge ("still unvisited children exist?").

#### ErrorHandler wrapper

```csharp
public sealed class ErrorHandler
{
    private readonly ErrorClassifier _classifier;
    private readonly ErrorStrategySelector _selector;
    private readonly RecoveryExecutor _executor;

    public ErrorHandler(
        ErrorClassifier? classifier = null,
        ErrorStrategySelector? selector = null,
        RecoveryExecutor? executor = null)
    {
        _classifier = classifier ?? new ErrorClassifier();
        _selector   = selector   ?? new ErrorStrategySelector();
        _executor   = executor   ?? new RecoveryExecutor();
    }

    /// <summary>
    /// HandleError — 3-step pipeline: classify → select → execute.
    /// Pipeline-level try/catch fallback → ErrorRecoveryResult(Abort, Failure, ...).
    /// </summary>
    public ErrorRecoveryResult HandleError(
        ErrorClassificationContext classificationCtx,
        StrategySelectionContext strategyCtx,
        Exception? exception = null)
    {
        try
        {
            // Step 1: Classify error
            var errorType = _classifier.Classify(classificationCtx);

            // Step 2: Select recovery strategy
            var strategy = _selector.SelectStrategy(errorType, strategyCtx);

            // Step 3: Execute recovery
            var recoveryCtx = new ErrorRecoveryContext(
                errorType, strategyCtx.RetryCount, exception);
            return _executor.Execute(strategy, recoveryCtx);
        }
        catch (Exception ex)
        {
            return new ErrorRecoveryResult(
                ErrorStrategy.Abort, RecoveryOutcome.Failure, 0,
                $"Unhandled exception during error handling: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
```

**Signature rationale**:
- `Exception? exception = null` is required because `ErrorClassificationContext` has `string? ExceptionType` (the type name) but NOT the actual `Exception` object. `ErrorRecoveryContext` needs `Exception?`. The exception must be passed separately.
- `ErrorRecoveryContext.RetryCount` takes `strategyCtx.RetryCount` (NOT `classificationCtx.RetryCount`). Reason: StrategySelectionContext.RetryCount is the authoritative source — it directly participates in strategy decision (IsApplicable: RetryCount < MaxRetries) and backoff calculation (min(2^retryCount, 10)). ErrorClassificationContext.RetryCount is a noise field (ErrorClassifier never reads it).

#### ErrorRecoveryResult extension

Add `string? Description = null` field to ErrorRecoveryResult for pipeline fallback diagnostic info:

```csharp
// Before
public sealed record class ErrorRecoveryResult(
    ErrorStrategy Strategy,
    RecoveryOutcome Outcome,
    double BackoffDelaySeconds);

// After
public sealed record class ErrorRecoveryResult(
    ErrorStrategy Strategy,
    RecoveryOutcome Outcome,
    double BackoffDelaySeconds,
    string? Description = null);
```

Rationale: ContainerActionResult and PopupHandlingResult both have Description fields. Without it, ErrorRecoveryResult pipeline fallback cannot preserve exception diagnostic info. `string? Description = null` is backward compatible (default null, existing constructors unaffected).

#### Pipeline fallback semantics

| Handler | Executor fallback | Pipeline fallback | Fallback semantics |
|---------|------------------|-------------------|--------------------|
| Popup | `PopupHandlingResult(false, "back_fallback")` | `PopupHandlingResult(false, "back_fallback")` — same Success=false | Press back |
| Container | `ContainerActionResult(Back, true, "...")` — executor knows BACK works | `ContainerActionResult(Back, false, "...")` — pipeline crashed, guessing | Press back |
| Error | `ErrorRecoveryResult(Abort, Failure, 0)` | `ErrorRecoveryResult(Abort, Failure, 0, "Unhandled exception...")` | Abort traversal |

**Container Success difference is intentional**: executor fallback = "I executed BACK, it works" (true). Pipeline fallback = "Pipeline crashed, BACK is safest guess" (false). This is documented here as a design decision, not a bug.

#### Sub-component classes unchanged

CompletionDetector, FallbackDecider, ContainerActionExecutor, ErrorClassifier, ErrorStrategySelector, RecoveryExecutor — no changes. They remain independent sealed classes, individually testable. Wrapper is orchestration layer only.

#### Engine integration — wiring, not refactor

No production code calls the 3 sub-components currently. TraversalFSM placeholder methods (`HandleFrameComplete`, `HandleErrorHandling`) will be wired to use the wrappers in Phase 2.3 full implementation. Current change establishes the wrapper entry points and dependency injection paths.

#### New tests

ContainerHandler and ErrorHandler wrappers each get 3 `[Fact]` tests:

1. Normal pipeline execution (detect→decide→execute chain)
2. Pipeline-level try/catch fallback (inject throwing sub-component)
3. Optional constructor injection (inject custom sub-component)

#### File placement

ContainerHandler wrapper class added to `ContainerHandler.cs` (same file as sub-components). ErrorHandler wrapper class added to `ErrorHandler.cs`. Names are currently unused — no collision.

### Documentation updates

- **handler-pipeline.md**: Pipeline orchestrator column updated for Container and Error; fallback chain updated from "1 layer" to "2 layers"; D-16 status → Fixed
- **decisions/log.md**: D-16 from `Deferred · Phase 2.3` → `Fixed`, commit hash filled

---

## Section 3: Trace Minimal (SpanType/PageTransition)

### Goal

Unlock D-E4's 2 TODO verification dimensions:
- `operation_rules`: needs restore_ops / skip_dangerous → requires Trace SpanType field
- `trace_integrity`: needs span_types / page_transitions → requires Trace PageTransition field

Scope: field additions only. No TraceCoordinator pipeline refactoring.

### Design

#### New enum: SpanType (11 values)

```csharp
namespace UniClaw.Core.Observability;

/// <summary>
/// Span 类型 — Trace 记录语义分类。
/// 对齐 Python expected_behavior.yaml 的 operation_rules/span_types 分类。
/// </summary>
public enum SpanType
{
    /// <summary>DFS 前进 (进入子节点)</summary>
    DfsForward,
    /// <summary>DFS 回退 (按返回键/弹出栈)</summary>
    DfsBacktrack,
    /// <summary>恢复操作 (restore_ops — toggle restore, slider restore)</summary>
    RestoreOp,
    /// <summary>跳过危险操作 (skip_dangerous — 不执行高风险 click/toggle)</summary>
    SkipDangerous,
    /// <summary>弹窗处理 (popup detect → dismiss)</summary>
    PopupHandling,
    /// <summary>容器完成 (completion detect → fallback action)</summary>
    ContainerHandling,
    /// <summary>错误恢复 (classify → strategy → execute)</summary>
    ErrorHandling,
    /// <summary>页面分析 (vision analyze → PageAnalysis)</summary>
    PageAnalysis,
    /// <summary>缓存操作 (page cache update/restore)</summary>
    CacheOp,
    /// <summary>AI 调用 (LLM/vision API)</summary>
    AICall,
    /// <summary>状态决策 (FSM transition reason)</summary>
    StateDecision,
}
```

11 values covering operation_rules and trace_integrity classification dimensions.

#### New record: PageTransition

```csharp
/// <summary>
/// 页面间导航记录 — distinct from FSM StateTransition.
/// StateTransition: Idle→Traversing (FSM state machine).
/// PageTransition: home→wifi (user-facing page navigation).
/// </summary>
public sealed record class PageTransition(
    string FromPage,
    string ToPage,
    string TransitionType,     // "forward", "back", "sub_page", "popup_dismiss"
    string? NodeId = null,
    double? DurationMs = null,
    DateTimeOffset Timestamp = default,
    Dictionary<string, object>? Metadata = null);
```

#### ExecutionRecord extension

Add `SpanType? SpanType = null` field (backward compatible):

```csharp
public sealed record class ExecutionRecord(
    string Action,
    string Status,
    SpanType? SpanType = null,         // ← NEW: semantic classification
    object? Target = null,
    double DurationMs = 0,
    DateTimeOffset Timestamp = default,
    Dictionary<string, object>? Metadata = null);
```

#### ITraceRecorder extension

Add 2 new methods:

```csharp
/// <summary>
/// 记录页面间导航
/// </summary>
Task RecordPageTransitionAsync(
    PageTransition transition,
    CancellationToken cancellationToken = default);

/// <summary>
/// 获取所有页面导航记录
/// </summary>
Task<List<PageTransition>> GetPageTransitionsAsync(
    CancellationToken cancellationToken = default);
```

#### TraceCoordinator — unchanged

14 empty-shell methods remain unchanged. Future integration will fill them:
- `RecordPageTransition(from, to, type)` → constructs PageTransition + calls RecordPageTransitionAsync
- `RecordActionExecution(action, target, success)` → constructs ExecutionRecord with SpanType annotation

#### Enum value lock

SpanType is a new enum. Register in:
- `constitution/locked-enums.md`: `SpanType = 11 值 (火山级)`
- `ArchitectureGuardTests`: `EnumValueGuardTests.SpanType_Has11Values`
- `decisions/log.md`: D-E8: SpanType 11 值锁定

#### D-E4 impact

| Dimension | Before | After |
|-----------|--------|-------|
| operation_rules | ⏳ TODO: blocked on Trace SpanType | ✅ ExecutionRecord.SpanType = RestoreOp/SkipDangerous verifiable |
| trace_integrity | ⏳ TODO: blocked on Trace PageTransition | ✅ SpanType enum + PageTransition record verifiable |

**Verification logic implementation is NOT in this change**. It belongs to a future change (ExpectedBehavior rule expansion).

---

## Implementation Order

1. Guard tests → 4 tests in 2 new inner classes + constitution/constraints.md updates
2. Handler wrapper → ContainerHandler + ErrorHandler + ErrorRecoveryResult.Description extension + 6 new tests + handler-pipeline.md + D-16 update
3. Trace minimal → SpanType enum + PageTransition record + ExecutionRecord.SpanType field + ITraceRecorder 2 new methods + SpanType_Has11Values guard + D-E8 decision + D-E4 impact note

Each step is independently testable: `dotnet test` should pass after each step with 0 failures.

---

## Correctness Issues Resolved During Design

| # | Issue | Resolution |
|---|-------|-----------|
| 1 | HandleError missing Exception? parameter | Added `Exception? exception = null` param |
| 2 | CompletionContext lacks NodeId/ITraversalContext | Added `string nodeId` + `ITraversalContext traversalContext` params to HandleContainer |
| 3 | Container pipeline vs executor fallback Success semantics differ | Documented as intentional: executor=true (known action), pipeline=false (crash guess) |
| 4 | "Engine refactor" mischaracterized | Corrected to "Engine wiring" — no existing calls to replace |
| 5 | CompletionContext cannot derive from ITraversalContext | Confirmed design: caller builds CompletionContext from engine state |
| 6 | RetryCount dual-source ambiguity (classificationCtx vs strategyCtx) | Documented: ErrorRecoveryContext.RetryCount takes strategyCtx.RetryCount (authoritative); classificationCtx.RetryCount is noise field |
| 7 | ErrorRecoveryResult lacks Description field | Added `string? Description = null` for pipeline fallback diagnostic info |
