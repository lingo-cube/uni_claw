using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Traversal;

namespace UniClaw.Core.StateMachine;

/// <summary>
/// OperationDispatcher — 内部 helper，将 Domain Operation (OperationType + TargetType) 派发到 IActionExecutor 方法。
/// NoAction → 跳过；Target null + 需要 target → throw InvalidOperationException。
/// </summary>
internal static class OperationDispatcher
{
    /// <summary>
    /// 执行单个 Operation，派发到 IActionExecutor 对应方法。
    /// </summary>
    /// <param name="operation">要执行的 Domain 操作</param>
    /// <param name="executor">IActionExecutor 实例</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>true 表示执行成功，false 表示执行失败（非异常）</returns>
    /// <exception cref="InvalidOperationException">Target 为 null 但 OperationType 需要 target 时抛出</exception>
    public static async Task<bool> DispatchAsync(
        Operation operation,
        IActionExecutor executor,
        CancellationToken ct = default)
    {
        return operation.Action switch
        {
            OperationType.NoAction => true, // No-op: skip executor call

            OperationType.Click => await DispatchClickAsync(operation, executor, ct),
            OperationType.Swipe => await DispatchSwipeAsync(operation, executor, ct),
            OperationType.Back => await DispatchBackAsync(executor, ct),
            OperationType.InputText => await DispatchInputTextAsync(operation, executor, ct),

            _ => throw new InvalidOperationException(
                $"Unknown OperationType: {operation.Action}")
        };
    }

    private static async Task<bool> DispatchClickAsync(
        Operation operation, IActionExecutor executor, CancellationToken ct)
    {
        var target = operation.Target
            ?? throw new InvalidOperationException("Click operation requires a Target");

        if (target.By != TargetType.Coordinate)
            throw new InvalidOperationException(
                $"Click operation requires Coordinate target, got {target.By}");

        var coord = target.Value as Coordinate
            ?? throw new InvalidOperationException(
                $"Target.Value must be a Coordinate for Click, got {target.Value?.GetType().Name ?? "null"}");

        return await executor.TapAsync(coord.X, coord.Y, ct);
    }

    private static async Task<bool> DispatchSwipeAsync(
        Operation operation, IActionExecutor executor, CancellationToken ct)
    {
        var target = operation.Target
            ?? throw new InvalidOperationException("Swipe operation requires a Target");

        if (target.By != TargetType.Coordinate)
            throw new InvalidOperationException(
                $"Swipe operation requires Coordinate target, got {target.By}");

        var startCoord = target.Value as Coordinate
            ?? throw new InvalidOperationException(
                $"Target.Value must be a Coordinate for Swipe start, got {target.Value?.GetType().Name ?? "null"}");

        // End coordinate from Params
        if (!operation.Params.TryGetValue("end_coordinate", out var endObj)
            || endObj is not Coordinate endCoord)
            throw new InvalidOperationException(
                "Swipe requires 'end_coordinate' (Coordinate) in Params");

        int durationMs = 300;
        if (operation.Params.TryGetValue("duration_ms", out var durObj) && durObj is int d)
            durationMs = d;

        return await executor.SwipeAsync(
            startCoord.X, startCoord.Y,
            endCoord.X, endCoord.Y,
            durationMs, ct);
    }

    private static async Task<bool> DispatchBackAsync(
        IActionExecutor executor, CancellationToken ct)
    {
        return await executor.PressBackAsync(ct);
    }

    private static async Task<bool> DispatchInputTextAsync(
        Operation operation, IActionExecutor executor, CancellationToken ct)
    {
        var target = operation.Target
            ?? throw new InvalidOperationException("InputText operation requires a Target");

        if (target.By != TargetType.Text)
            throw new InvalidOperationException(
                $"InputText operation requires Text target, got {target.By}");

        var text = target.Value?.ToString()
            ?? throw new InvalidOperationException(
                "Target.Value must be a non-null string for InputText");

        return await executor.InputTextAsync(text, ct);
    }
}
