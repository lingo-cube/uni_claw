# Design: DismissStrategy + UrgencyLevel Python Alignment

## D-10: Conditional Dismiss Strategy

### Current (wrong)
```csharp
public static readonly IReadOnlyDictionary<PopupType, DismissStrategy> DismissStrategyMap = ...
// Permission → AutoClose (always), Error → Back (always), Ad → WaitTimeout (always)
```

### Target (aligned with Python)
```csharp
private DismissStrategy DetermineDismissStrategy(PopupType popupType, string? dismissTarget)
{
    // Python: if self._find_dismiss_target(ui_elements, popup_type): return "auto_close"
    if (dismissTarget is not null)
        return DismissStrategy.AutoClose;

    // Python: fallback per popup type when no dismiss target found
    return popupType switch
    {
        PopupType.Ad        => DismissStrategy.Back,
        PopupType.Permission => DismissStrategy.WaitTimeout,
        PopupType.Error     => DismissStrategy.AutoCloseOrBack,
        _                   => DismissStrategy.Back
    };
}
```

### PopupActionExecutor sync
All 5 Default methods changed from static strategy strings to conditional logic:
- `ctx.Classification.DismissTarget is not null` → "auto_close" (Success: true)
- No target → type-specific fallback action (Success: false for Ad/Permission/Dialog/Unknown; true for Error)

## D-11: Remove UrgencyLevel.Critical

### Current
```csharp
public enum UrgencyLevel { Low, Medium, High, Critical }  // 4 values, Critical unreachable
```

### Target
```csharp
public enum UrgencyLevel { Low, Medium, High }  // 3 values, aligned with Python
```

Guard test: `UrgencyLevel_Has4Values` → `UrgencyLevel_Has3Values`
locked-enums.md: value count 4 → 3, Guard name updated

## D-12: No Error in CompletionReason (decision only)

Python CompletionStatus.ERROR is declared but never assigned by CompletionDetector.detect_completion(). Adding a dead value contradicts the D-11 principle of removing dead values. Deferred until ErrorHandler implements a direct completion path that bypasses CompletionDetector.

## D-13: Keep PreconditionCheck→Branch removed (decision only)

Python `_handle_precondition_check()` only returns EXECUTE or ERROR_HANDLING. The BRANCH transition exists in VALID_TRANSITIONS but is unused. C# removal (D-1) is correct tightening.
