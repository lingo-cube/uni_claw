namespace UniClaw.Core.StateMachine.Cache;

/// <summary>
/// Cache context read-only interface — Cache and configuration state.
/// 只读属性暴露，mutation 方法只在 concrete class。
/// </summary>
public interface ICacheContext
{
    /// <summary>页面缓存</summary>
    IReadOnlyDictionary<string, object> PageCache { get; }

    /// <summary>缓存是否有效</summary>
    bool CacheValid { get; }
}
