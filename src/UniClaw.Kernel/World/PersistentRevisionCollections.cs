using System.Collections;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace UniClaw.Kernel.World;

/// <summary>
/// Persistent owner-internal map with a canonical public enumeration projection.
/// Lookups use structurally shared storage. Enumeration is materialized lazily
/// by replaying the previous revision's FrozenDictionary input order plus this
/// revision's new keys, exactly matching the pre-WMP publication algorithm.
/// WMP-002: the canonical frozen layout depends on the construction input
/// sequence (frozen ordering is not a pure function of the key set), so a
/// revision's view is always built from its parent's materialized layout.
/// Ancestors are computed transiently during that build and are never cached:
/// retention stays proportional to what was actually enumerated, and no
/// hidden ancestor-view memory is retained by touching one revision.
/// </summary>
internal sealed class PersistentRevisionDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    private readonly ImmutableDictionary<TKey, TValue> _storage;
    private readonly PersistentRevisionDictionary<TKey, TValue>? _canonicalParent;
    private readonly IReadOnlyList<TKey> _addedKeys;
    private readonly IReadOnlyList<TKey> _changedKeys;
    private FrozenDictionary<TKey, TValue>? _canonicalView;

    private PersistentRevisionDictionary(
        ImmutableDictionary<TKey, TValue> storage,
        PersistentRevisionDictionary<TKey, TValue>? canonicalParent,
        IReadOnlyList<TKey> addedKeys,
        IReadOnlyList<TKey> changedKeys)
    {
        _storage = storage;
        _canonicalParent = canonicalParent;
        _addedKeys = addedKeys;
        _changedKeys = changedKeys;
    }

    internal static Builder Next(
        PersistentRevisionDictionary<TKey, TValue>? parent,
        IEqualityComparer<TKey> comparer) => new(parent, comparer);

    public int Count => _storage.Count;
    public IEnumerable<TKey> Keys => CanonicalView.Keys;
    public IEnumerable<TValue> Values => CanonicalView.Values;
    public TValue this[TKey key] => _storage[key];
    public bool ContainsKey(TKey key) => _storage.ContainsKey(key);
    public bool TryGetValue(TKey key, out TValue value) => _storage.TryGetValue(key, out value!);
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => CanonicalView.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal IEnumerable<KeyValuePair<TKey, TValue>> ChangedEntries =>
        _changedKeys.Select(key => new KeyValuePair<TKey, TValue>(key, _storage[key]));

    private FrozenDictionary<TKey, TValue> CanonicalView =>
        _canonicalView ??= BuildCanonicalView();

    /// <summary>
    /// Iteratively materialize the uncached ancestor prefix root-down; the
    /// intermediate ancestor views live only in locals and are never stored
    /// on the ancestor objects (explicit stack keeps arbitrarily deep
    /// revision chains off the call stack).
    /// </summary>
    private FrozenDictionary<TKey, TValue> BuildCanonicalView()
    {
        // UAP-001 缺陷修复：transient 重建必须以 pending 链切断处的
        // 【已缓存】祖先视图为基——原实现取 `_canonicalParent?._canonicalView`
        //（最近祖先，此时必为 null），等价于从 null 重建，静默丢弃该缓存
        // 祖先已建立的全部键，并把错误视图经 ??= 永久缓存。触发面：中途
        // 枚举过中间 revision（缓存视图）后出现仅 SetItem（无新增键）的
        // revision，再枚举最新 revision（Count/TryGetValue 走 storage 不受
        // 影响，故既有测试未暴露）。瞬态祖先内存策略不变：中间视图仍只
        // 存在于局部变量，仅 self 经 ??= 缓存。
        var pending = new Stack<PersistentRevisionDictionary<TKey, TValue>>();
        PersistentRevisionDictionary<TKey, TValue>? cachedAncestor = null;
        for (var cursor = _canonicalParent; cursor is not null; cursor = cursor._canonicalParent)
        {
            if (cursor._canonicalView is not null)
            {
                cachedAncestor = cursor;
                break;
            }
            pending.Push(cursor);
        }
        var parentView = cachedAncestor?._canonicalView;
        while (pending.TryPop(out var ancestor))
            parentView = ancestor.BuildFrom(parentView);
        return BuildFrom(parentView);
    }

    private FrozenDictionary<TKey, TValue> BuildFrom(
        FrozenDictionary<TKey, TValue>? parentView)
    {
        var order = parentView is null ? _addedKeys : parentView.Keys.Concat(_addedKeys);
        return order
            .Select(key => new KeyValuePair<TKey, TValue>(key, _storage[key]))
            .ToFrozenDictionary(_storage.KeyComparer);
    }

    internal sealed class Builder
    {
        private readonly PersistentRevisionDictionary<TKey, TValue>? _parent;
        private readonly List<TKey> _addedKeys = new();
        private readonly List<TKey> _changedKeys = new();
        private ImmutableDictionary<TKey, TValue> _storage;
        private bool _changed;

        internal Builder(
            PersistentRevisionDictionary<TKey, TValue>? parent,
            IEqualityComparer<TKey> comparer)
        {
            _parent = parent;
            _storage = parent?._storage ?? ImmutableDictionary.Create<TKey, TValue>(comparer);
        }

        internal bool ContainsKey(TKey key) => _storage.ContainsKey(key);
        internal bool TryGetValue(TKey key, out TValue value) => _storage.TryGetValue(key, out value!);

        internal void Add(TKey key, TValue value)
        {
            _storage = _storage.Add(key, value);
            _addedKeys.Add(key);
            _changedKeys.Add(key);
            _changed = true;
        }

        internal void SetItem(TKey key, TValue value)
        {
            if (!_storage.ContainsKey(key))
                _addedKeys.Add(key);
            if (!_changedKeys.Contains(key))
                _changedKeys.Add(key);
            _storage = _storage.SetItem(key, value);
            _changed = true;
        }

        internal PersistentRevisionDictionary<TKey, TValue> Build() =>
            !_changed && _parent is not null
                ? _parent
                : new PersistentRevisionDictionary<TKey, TValue>(
                    _storage, _parent, _addedKeys.ToArray(), _changedKeys.ToArray());
    }
}

/// <summary>
/// Persistent owner-internal set with the pre-WMP FrozenSet enumeration shape.
/// Contains is served from shared storage; canonical enumeration is paid only by
/// a consumer that enumerates the public EvidenceBasis. WMP-002: same transient
/// ancestor policy as PersistentRevisionDictionary — a revision's view is built
/// from its parent's materialized layout; ancestors are never cached, so no
/// hidden ancestor-view memory is retained by enumerating one revision.
/// </summary>
internal sealed class PersistentRevisionSet<T> : IReadOnlySet<T>
    where T : notnull
{
    private readonly ImmutableHashSet<T> _storage;
    private readonly PersistentRevisionSet<T>? _canonicalParent;
    private readonly IReadOnlyList<T> _addedItems;
    private FrozenSet<T>? _canonicalView;

    private PersistentRevisionSet(
        ImmutableHashSet<T> storage,
        PersistentRevisionSet<T>? canonicalParent,
        IReadOnlyList<T> addedItems)
    {
        _storage = storage;
        _canonicalParent = canonicalParent;
        _addedItems = addedItems;
    }

    internal static Builder Next(
        PersistentRevisionSet<T>? parent,
        IEqualityComparer<T> comparer) => new(parent, comparer);

    public int Count => _storage.Count;
    public bool Contains(T item) => _storage.Contains(item);
    public bool IsProperSubsetOf(IEnumerable<T> other) => _storage.IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<T> other) => _storage.IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<T> other) => _storage.IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<T> other) => _storage.IsSupersetOf(other);
    public bool Overlaps(IEnumerable<T> other) => _storage.Overlaps(other);
    public bool SetEquals(IEnumerable<T> other) => _storage.SetEquals(other);
    public IEnumerator<T> GetEnumerator() => CanonicalView.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private FrozenSet<T> CanonicalView =>
        _canonicalView ??= BuildCanonicalView();

    /// <summary>Same iterative transient-ancestor policy as the dictionary
    ///（含 UAP-001 修复：以 pending 链切断处的已缓存祖先视图为重建基）。</summary>
    private FrozenSet<T> BuildCanonicalView()
    {
        var pending = new Stack<PersistentRevisionSet<T>>();
        PersistentRevisionSet<T>? cachedAncestor = null;
        for (var cursor = _canonicalParent; cursor is not null; cursor = cursor._canonicalParent)
        {
            if (cursor._canonicalView is not null)
            {
                cachedAncestor = cursor;
                break;
            }
            pending.Push(cursor);
        }
        var parentView = cachedAncestor?._canonicalView;
        while (pending.TryPop(out var ancestor))
            parentView = ancestor.BuildFrom(parentView);
        return BuildFrom(parentView);
    }

    private FrozenSet<T> BuildFrom(FrozenSet<T>? parentView)
    {
        var order = parentView is null ? _addedItems : parentView.Concat(_addedItems);
        return order.ToFrozenSet(_storage.KeyComparer);
    }

    internal sealed class Builder
    {
        private readonly PersistentRevisionSet<T>? _parent;
        private readonly List<T> _addedItems = new();
        private ImmutableHashSet<T> _storage;

        internal Builder(PersistentRevisionSet<T>? parent, IEqualityComparer<T> comparer)
        {
            _parent = parent;
            _storage = parent?._storage ?? ImmutableHashSet.Create<T>(comparer);
        }

        internal void Add(T item)
        {
            if (!_storage.Contains(item))
                _addedItems.Add(item);
            _storage = _storage.Add(item);
        }

        internal PersistentRevisionSet<T> Build() =>
            new(_storage, _parent, _addedItems.ToArray());
    }
}
