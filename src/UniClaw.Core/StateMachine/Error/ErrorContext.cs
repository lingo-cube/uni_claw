using System.Collections.ObjectModel;
using UniClaw.Core.Observability;
using UniClaw.Core.Traversal;

namespace UniClaw.Core.StateMachine.Error;

/// <summary>
/// Error context — Error tracking and recovery state.
/// 5 个字段封装所有错误相关状态：失败节点、连续错误、重试计数、异常链。
/// </summary>
public sealed class ErrorContext : IErrorContext
{
    // --- 5 private fields ---
    private readonly Dictionary<string, ErrorRecord> _failedNodes;
    private int _consecutiveErrors;
    private int _retryCount;
    private Exception? _lastError;
    private List<Exception>? _exceptionChain;

    /// <summary>构造 ErrorContext</summary>
    public ErrorContext()
    {
        _failedNodes = new Dictionary<string, ErrorRecord>();
        _consecutiveErrors = 0;
        _retryCount = 0;
        _lastError = null;
        _exceptionChain = null;
    }

    // --- IErrorContext implementation ---

    /// <inheritdoc />
    public IReadOnlyDictionary<string, ErrorRecord> FailedNodes => _failedNodes;

    /// <inheritdoc />
    public int ConsecutiveErrors => _consecutiveErrors;

    /// <inheritdoc />
    public int RetryCount => _retryCount;

    /// <inheritdoc />
    public Exception? LastError
    {
        get => _lastError;
        set => _lastError = value;
    }

    /// <inheritdoc />
    public IReadOnlyList<Exception>? ExceptionChain => _exceptionChain;

    // --- Mutation methods (engine-only) ---

    /// <summary>递增连续错误计数</summary>
    public void IncrementConsecutiveErrors() => _consecutiveErrors++;

    /// <summary>重置连续错误计数为 0</summary>
    public void ResetConsecutiveErrors() => _consecutiveErrors = 0;

    /// <summary>递增重试计数</summary>
    public void IncrementRetryCount() => _retryCount++;

    /// <summary>添加失败节点</summary>
    public void AddFailedNode(string nodeId, ErrorRecord error) => _failedNodes[nodeId] = error;

    /// <summary>设置异常链</summary>
    public void SetExceptionChain(List<Exception>? value) => _exceptionChain = value;
}
