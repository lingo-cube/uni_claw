using UniClaw.Core.Observability;
using UniClaw.Core.Traversal;

namespace UniClaw.Core.StateMachine.Error;

/// <summary>
/// Error context read-only interface — Error tracking and recovery state.
/// 只读属性暴露，mutation 方法只在 concrete class。
/// </summary>
public interface IErrorContext
{
    /// <summary>失败节点映射</summary>
    IReadOnlyDictionary<string, ErrorRecord> FailedNodes { get; }

    /// <summary>连续错误计数</summary>
    int ConsecutiveErrors { get; }

    /// <summary>重试计数</summary>
    int RetryCount { get; }

    /// <summary>最近一次异常</summary>
    Exception? LastError { get; }

    /// <summary>异常链</summary>
    IReadOnlyList<Exception>? ExceptionChain { get; }
}
