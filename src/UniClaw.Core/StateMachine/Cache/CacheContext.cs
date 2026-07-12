namespace UniClaw.Core.StateMachine.Cache;

/// <summary>
/// Cache context — Cache and configuration state.
/// 2 core fields + 2 Phase 3 reserved fields.
/// </summary>
public sealed class CacheContext : ICacheContext
{
    // --- 2 core private fields ---
    private readonly Dictionary<string, object> _pageCache;
    private bool _cacheValid;

    // --- Phase 3 reserved fields ---
    private object? _scrollHandler;     // Phase 3: scroll state management
    private object? _currentSnapshot;   // Phase 3: page snapshot management

    /// <summary>构造 CacheContext</summary>
    public CacheContext()
    {
        _pageCache = new Dictionary<string, object>();
        _cacheValid = false;
        _scrollHandler = null;
        _currentSnapshot = null;
    }

    // --- ICacheContext implementation ---

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object> PageCache => _pageCache;

    /// <inheritdoc />
    public bool CacheValid => _cacheValid;

    // --- Mutation methods (engine-only) ---

    /// <summary>设置缓存有效</summary>
    public void SetCacheValid(bool value) => _cacheValid = value;

    // --- PageCache internal access (for mutation) ---

    /// <summary>获取页面缓存内部字典 (用于修改)</summary>
    public Dictionary<string, object> GetPageCacheInternal() => _pageCache;
}
