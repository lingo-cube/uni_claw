using System.Collections.Immutable;
using System.Text.RegularExpressions;
using UniClaw.Core.Domain;
using UniClaw.Core.Graph.Models;

namespace UniClaw.Core.StateMachine;

/// <summary>
/// PopupType enum (5 值) — 对齐 Python popup 分类。
/// </summary>
public enum PopupType
{
    Permission, Error, Ad, Dialog, Unknown
}

/// <summary>
/// UrgencyLevel enum (3 值) — 对齐 Python UrgencyLevel (LOW/MEDIUM/HIGH)，无 Critical。
/// </summary>
public enum UrgencyLevel
{
    Low, Medium, High
}

/// <summary>
/// BlockingType enum (3 值) — MODAL/NON_MODAL/TOAST。
/// </summary>
public enum BlockingType
{
    Modal, NonModal, Toast
}

/// <summary>
/// DismissStrategy enum (4 值) — 对齐 Python dismiss strategy。
/// </summary>
public enum DismissStrategy
{
    AutoClose, Back, WaitTimeout, AutoCloseOrBack
}

/// <summary>
/// PopupDetector — regex pattern matching (4 popup types, case-insensitive)。
/// </summary>
public sealed class PopupDetector
{
    /// <summary>
    /// Pattern registry — 4 popup types × 5-6 regex patterns each。
    /// </summary>
    public static readonly IReadOnlyDictionary<PopupType, ImmutableArray<string>> PatternRegistry =
        new Dictionary<PopupType, ImmutableArray<string>>
        {
            [PopupType.Permission] = ImmutableArray.Create(
                "allow", "grant", "permission", "access", "authorize", "consent"),
            [PopupType.Error] = ImmutableArray.Create(
                "error", "failed", "crash", "unfortunately", "something went wrong", "try again"),
            [PopupType.Ad] = ImmutableArray.Create(
                "advertisement", "sponsored", "promo", "ad", "skip ad", "remove ads"),
            [PopupType.Dialog] = ImmutableArray.Create(
                "confirm", "cancel", "agree", "terms", "ok", "dialog"),
        };

    /// <summary>
    /// 检测弹窗类型 — regex pattern matching (case-insensitive)。
    /// Priority: Permission > Error > Ad > Dialog。无匹配 → Unknown。
    /// </summary>
    public PopupType Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return PopupType.Unknown;

        var lowerText = text.ToLowerInvariant();

        // Priority order: Permission > Error > Ad > Dialog
        var priorityOrder = new[] { PopupType.Permission, PopupType.Error, PopupType.Ad, PopupType.Dialog };

        foreach (var popupType in priorityOrder)
        {
            if (PatternRegistry.TryGetValue(popupType, out var patterns))
            {
                foreach (var pattern in patterns)
                {
                    if (Regex.IsMatch(lowerText, pattern, RegexOptions.IgnoreCase))
                        return popupType;
                }
            }
        }

        return PopupType.Unknown;
    }
}

/// <summary>
/// PopupClassifier — 5 sub-methods: determine_type → find_dismiss → strategy → urgency → blocking。
/// </summary>
public sealed class PopupClassifier
{
    /// <summary>
    /// Dismiss button priorities per popup type。
    /// </summary>
    public static readonly IReadOnlyDictionary<PopupType, ImmutableArray<string>> DismissButtonPriorities =
        new Dictionary<PopupType, ImmutableArray<string>>
        {
            [PopupType.Permission] = ImmutableArray.Create("allow", "accept", "continue", "grant", "ok"),
            [PopupType.Error] = ImmutableArray.Create("ok", "close", "dismiss", "acknowledge"),
            [PopupType.Ad] = ImmutableArray.Create("close", "skip", "x", "dismiss"),
            [PopupType.Dialog] = ImmutableArray.Create("ok", "cancel", "close", "yes", "no"),
            [PopupType.Unknown] = ImmutableArray.Create("ok", "close", "back"),
        };

    /// <summary>
    /// 分类弹窗 — 5 sub-methods 顺序执行。
    /// D-10: DetermineDismissStrategy 改为条件逻辑对齐 Python。
    /// </summary>
    public PopupClassification Classify(string popupText, List<string>? availableButtons = null)
    {
        // 1. Determine popup type
        var popupType = new PopupDetector().Detect(popupText);

        // 2. Find dismiss target
        var dismissTarget = FindDismissTarget(popupType, availableButtons);

        // 3. Determine dismiss strategy (D-10: conditional logic aligned with Python)
        var dismissStrategy = DetermineDismissStrategy(popupType, dismissTarget);

        // 4. Determine urgency
        var urgency = DetermineUrgency(popupType, popupText);

        // 5. Determine blocking type
        var blockingType = DetermineBlockingType(popupType, popupText);

        return new PopupClassification(
            PopupType: popupType,
            DismissTarget: dismissTarget,
            DismissStrategy: dismissStrategy,
            Urgency: urgency,
            BlockingType: blockingType);
    }

    private string? FindDismissTarget(PopupType popupType, List<string>? availableButtons)
    {
        if (availableButtons == null || availableButtons.Count == 0)
            return null;

        if (!DismissButtonPriorities.TryGetValue(popupType, out var priorities))
            return null;

        var lowerButtons = availableButtons.Select(b => b.ToLowerInvariant()).ToList();
        foreach (var priority in priorities)
        {
            if (lowerButtons.Contains(priority.ToLowerInvariant()))
                return priority;
        }

        return null;
    }

    /// <summary>
    /// D-10: 条件逻辑对齐 Python PopupClassifier._determine_dismiss_strategy。
    /// 有 dismiss target → 统一 AutoClose; 无 target → 按 PopupType fallback。
    /// </summary>
    private DismissStrategy DetermineDismissStrategy(PopupType popupType, string? dismissTarget)
    {
        // Python: if self._find_dismiss_target(ui_elements, popup_type): return "auto_close"
        if (dismissTarget is not null)
            return DismissStrategy.AutoClose;

        // Python: fallback per popup type when no dismiss target found
        return popupType switch
        {
            PopupType.Ad        => DismissStrategy.Back,              // Python: "back"
            PopupType.Permission => DismissStrategy.WaitTimeout,      // Python: "wait_timeout"
            PopupType.Error     => DismissStrategy.AutoCloseOrBack,   // Python: "auto_close_or_back"
            _                   => DismissStrategy.Back               // Python: "back" (Dialog, Unknown)
        };
    }

    private UrgencyLevel DetermineUrgency(PopupType popupType, string text)
    {
        return popupType switch
        {
            PopupType.Permission => UrgencyLevel.High,
            PopupType.Error => UrgencyLevel.Medium,
            PopupType.Ad => UrgencyLevel.Low,
            PopupType.Dialog => UrgencyLevel.Medium,
            _ => UrgencyLevel.Low
        };
    }

    private BlockingType DetermineBlockingType(PopupType popupType, string text)
    {
        return popupType switch
        {
            PopupType.Permission => BlockingType.Modal,
            PopupType.Error => BlockingType.Modal,
            PopupType.Ad => BlockingType.NonModal,
            PopupType.Dialog => BlockingType.Modal,
            _ => BlockingType.Modal
        };
    }
}

/// <summary>弹窗分类结果</summary>
public sealed record class PopupClassification(
    PopupType PopupType,
    string? DismissTarget,
    DismissStrategy DismissStrategy,
    UrgencyLevel Urgency,
    BlockingType BlockingType);

/// <summary>
/// PopupActionExecutor — Hook Dispatch 表 (5 PopupType hooks) + 异常兜底到 back。
/// D-10: Default 方法对齐 Python 条件逻辑 — 有 dismiss target → auto_close, 无 → type fallback。
/// </summary>
public sealed class PopupActionExecutor
{
    private readonly Dictionary<PopupType, Func<PopupContext, PopupHandlingResult>> _dispatchTable;

    public PopupActionExecutor(
        Func<PopupContext, PopupHandlingResult>? permissionHook = null,
        Func<PopupContext, PopupHandlingResult>? errorHook = null,
        Func<PopupContext, PopupHandlingResult>? adHook = null,
        Func<PopupContext, PopupHandlingResult>? dialogHook = null,
        Func<PopupContext, PopupHandlingResult>? unknownHook = null)
    {
        _dispatchTable = new Dictionary<PopupType, Func<PopupContext, PopupHandlingResult>>
        {
            [PopupType.Permission] = permissionHook ?? DefaultPermission,
            [PopupType.Error] = errorHook ?? DefaultError,
            [PopupType.Ad] = adHook ?? DefaultAd,
            [PopupType.Dialog] = dialogHook ?? DefaultDialog,
            [PopupType.Unknown] = unknownHook ?? DefaultUnknown,
        };
    }

    /// <summary>
    /// 执行弹窗处理 — Hook Dispatch + 异常兜底到 back。
    /// </summary>
    public PopupHandlingResult Execute(PopupType popupType, PopupContext ctx)
    {
        try
        {
            if (_dispatchTable.TryGetValue(popupType, out var hook))
                return hook(ctx);
            return DefaultUnknown(ctx);
        }
        catch (Exception)
        {
            // Exception fallback to back navigation
            return new PopupHandlingResult(false, "back_fallback", "Exception during popup handling");
        }
    }

    /// <summary>
    /// D-10: 有 dismiss target → auto_close; 无 → type fallback。
    /// 对齐 Python _determine_dismiss_strategy + _handle_popup_state 行为。
    /// </summary>
    private static PopupHandlingResult DefaultPermission(PopupContext ctx)
    {
        if (ctx.Classification.DismissTarget is not null)
            return new PopupHandlingResult(true, "auto_close", "Clicked dismiss button for permission popup");
        return new PopupHandlingResult(false, "wait_timeout", "No dismiss target — waiting for permission popup to expire");
    }

    private static PopupHandlingResult DefaultError(PopupContext ctx)
    {
        if (ctx.Classification.DismissTarget is not null)
            return new PopupHandlingResult(true, "auto_close", "Clicked dismiss button for error popup");
        return new PopupHandlingResult(true, "auto_close_or_back", "Auto-closed or backed out of error popup");
    }

    private static PopupHandlingResult DefaultAd(PopupContext ctx)
    {
        if (ctx.Classification.DismissTarget is not null)
            return new PopupHandlingResult(true, "auto_close", "Clicked dismiss button for ad popup");
        return new PopupHandlingResult(false, "back", "No dismiss target — backed out of ad popup");
    }

    private static PopupHandlingResult DefaultDialog(PopupContext ctx)
    {
        if (ctx.Classification.DismissTarget is not null)
            return new PopupHandlingResult(true, "auto_close", "Clicked dismiss button for dialog popup");
        return new PopupHandlingResult(false, "back", "No dismiss target — backed out of dialog popup");
    }

    private static PopupHandlingResult DefaultUnknown(PopupContext ctx)
    {
        if (ctx.Classification.DismissTarget is not null)
            return new PopupHandlingResult(true, "auto_close", "Clicked dismiss button for unknown popup");
        return new PopupHandlingResult(false, "back", "No dismiss target — backed out of unknown popup");
    }
}

/// <summary>弹窗处理上下文</summary>
public sealed record class PopupContext(
    PopupClassification Classification,
    ITraversalContext TraversalContext);

/// <summary>弹窗处理结果</summary>
public sealed record class PopupHandlingResult(
    bool Success,
    string Action,
    string Description);

/// <summary>
/// StateRestorer — preserve/restore/validate lifecycle。
/// </summary>
public sealed class StateRestorer
{
    private readonly Dictionary<string, PreservedState> _preservedStates = new();

    /// <summary>
    /// 保存遍历状态 — 弹窗处理前调用。
    /// H-6: 保存完整 stack 内容 (所有 StackFrames), 不只是 depth。
    /// </summary>
    public string PreserveState(ITraversalContext context)
    {
        var stateId = Guid.NewGuid().ToString("N");

        // Save complete stack contents (all IStackFrame objects)
        var frames = new List<IStackFrame>();
        for (int i = 0; i < context.NodeStack.Depth; i++)
        {
            var frame = context.NodeStack.Peek(i);
            if (frame != null)
                frames.Add(frame);
        }

        var preserved = new PreservedState(
            StateId: stateId,
            CurrentNodeId: context.CurrentFrame?.NodeId,
            NodeStackFrames: frames,
            CurrentState: context.GlobalState,
            ExecutionResult: context.LastError?.Message,
            Timestamp: DateTimeOffset.UtcNow);
        _preservedStates[stateId] = preserved;
        return stateId;
    }

    /// <summary>
    /// 恢复遍历状态 — 弹窗处理后调用。
    /// H-7: 恢复全部 5 字段 (CurrentFrame, NodeStack, GlobalState, LastError, ExecutionResult)。
    /// </summary>
    public void RestoreState(string stateId, ITraversalContext context)
    {
        if (!_preservedStates.TryGetValue(stateId, out var preserved))
            return; // No preserved state to restore

        // Cast to concrete for mutation methods
        if (context is not TraversalRuntimeContext rtc)
            throw new InvalidOperationException("Context must be TraversalRuntimeContext for mutation");

        // 1. Restore CurrentFrame (from NodeStackFrames top)
        if (preserved.NodeStackFrames.Count > 0)
        {
            rtc.SetCurrentFrame(preserved.NodeStackFrames[0].Node);
        }

        // 2. Restore NodeStack (clear and rebuild from preserved frames — bottom-first)
        rtc.NodeStack.Clear();
        // Preserved frames are in top-to-bottom order (Peek(0)=top); push bottom-first to restore correct order
        foreach (var frame in Enumerable.Reverse(preserved.NodeStackFrames))
        {
            rtc.NodeStack.Push(frame.Node, frame.Children?.ToList());
        }

        // 3. Restore GlobalState
        rtc.SetGlobalState(preserved.CurrentState);

        // 4. Restore LastError (from ExecutionResult)
        rtc.SetLastError(preserved.ExecutionResult != null
            ? new Exception(preserved.ExecutionResult)
            : null);
    }

    /// <summary>
    /// 验证恢复后的状态完整性 — H-7: 比较恢复值与保存值 (不只是结构性检查)。
    /// </summary>
    public StateValidationResult ValidateRestoredState(ITraversalContext context, string? stateId = null)
    {
        var errors = new List<string>();

        // Basic structural checks
        if (context.CurrentFrame?.NodeId == null || string.IsNullOrWhiteSpace(context.CurrentFrame.NodeId))
            errors.Add("current_node_id is null or empty");

        if (context.NodeStack.Depth < 1)
            errors.Add("node_stack contains less than 1 entry");

        if (!Enum.IsDefined(context.GlobalState))
            errors.Add($"invalid GlobalState: {context.GlobalState}");

        // H-7: Compare restored values against preserved values (not just structural checks)
        if (stateId != null && _preservedStates.TryGetValue(stateId, out var preserved))
        {
            // Compare CurrentFrame.NodeId against preserved current_node_id
            if (context.CurrentFrame?.NodeId != preserved.CurrentNodeId)
                errors.Add($"current_node_id mismatch: restored={context.CurrentFrame?.NodeId}, preserved={preserved.CurrentNodeId}");

            // Compare GlobalState against preserved state
            if (context.GlobalState != preserved.CurrentState)
                errors.Add($"GlobalState mismatch: restored={context.GlobalState}, preserved={preserved.CurrentState}");

            // Compare NodeStack depth against preserved stack frames count
            if (context.NodeStack.Depth != preserved.NodeStackFrames.Count)
                errors.Add($"NodeStack depth mismatch: restored={context.NodeStack.Depth}, preserved={preserved.NodeStackFrames.Count}");
        }

        return errors.Count == 0
            ? new StateValidationResult(true, ImmutableArray<string>.Empty)
            : new StateValidationResult(false, ImmutableArray.CreateRange(errors));
    }
}

/// <summary>保存的状态</summary>
internal sealed record class PreservedState(
    string StateId,
    string? CurrentNodeId,
    List<IStackFrame> NodeStackFrames,
    GlobalState CurrentState,
    string? ExecutionResult,
    DateTimeOffset Timestamp);

/// <summary>状态验证结果</summary>
public sealed record class StateValidationResult(
    bool IsValid,
    ImmutableArray<string> Errors);

/// <summary>
/// PopupHandler — 6-step handle_popup() 流程:
/// detect → classify → preserve → handle → restore → validate。
/// </summary>
public sealed class PopupHandler
{
    private readonly PopupDetector _detector = new();
    private readonly PopupClassifier _classifier = new();
    private readonly StateRestorer _restorer = new();
    private readonly PopupActionExecutor _executor;

    private int _detectedCount;
    private int _handledCount;
    private readonly Dictionary<PopupType, int> _handlingStatistics = new();

    /// <summary>构造 PopupHandler</summary>
    public PopupHandler(PopupActionExecutor? executor = null)
    {
        _executor = executor ?? new PopupActionExecutor();
    }

    /// <summary>
    /// 6-step handle_popup() 流程 — 严格顺序执行，每步完成后才进入下一步。
    /// H-8: 顶层 try-catch 兜底到 back_fallback。
    /// </summary>
    public PopupHandlingResult HandlePopup(string popupText, ITraversalContext context, List<string>? availableButtons = null)
    {
        try
        {
            // Step 1: detect
            var popupType = _detector.Detect(popupText);
            if (popupType == PopupType.Unknown && string.IsNullOrWhiteSpace(popupText))
                return new PopupHandlingResult(false, "no_popup", "No popup detected");

            _detectedCount++;
            _handlingStatistics[popupType] = _handlingStatistics.GetValueOrDefault(popupType) + 1;

            // Step 2: classify
            var classification = _classifier.Classify(popupText, availableButtons);

            // Step 3: preserve
            var stateId = _restorer.PreserveState(context);

            // Step 4: handle (dispatch)
            var popupContext = new PopupContext(classification, context);
            var handlingResult = _executor.Execute(classification.PopupType, popupContext);

            // Step 5: restore
            _restorer.RestoreState(stateId, context);

            // Step 6: validate (with stateId for value comparison)
            var validation = _restorer.ValidateRestoredState(context, stateId);
            if (!validation.IsValid)
            {
                return new PopupHandlingResult(false, "validation_failed",
                    $"State validation failed: {string.Join(", ", validation.Errors)}");
            }

            if (handlingResult.Success)
                _handledCount++;

            return handlingResult;
        }
        catch (Exception ex)
        {
            // H-8: Top-level exception fallback — any step exception → back_fallback
            return new PopupHandlingResult(false, "back_fallback",
                $"Unhandled exception during popup handling: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>处理统计</summary>
    public PopupHandlerStatistics GetStatistics()
        => new PopupHandlerStatistics(_detectedCount, _handledCount,
            new Dictionary<PopupType, int>(_handlingStatistics));
}

/// <summary>弹窗处理统计</summary>
public sealed record class PopupHandlerStatistics(
    int DetectedCount,
    int HandledCount,
    Dictionary<PopupType, int> HandlingStatistics)
{
    /// <summary>处理率</summary>
    public double HandlingRate => DetectedCount > 0 ? HandledCount / (double)DetectedCount : 0.0;
}
