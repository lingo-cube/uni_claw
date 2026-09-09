using System.Collections;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace UniClaw.Kernel.World;

/// <summary>
/// Persistent owner-internal map with a canonical public enumeration projection.
/// Lookups use structurally shared storage. Enumeration is materialized lazily by
/// replaying the previous revision's FrozenDictionary input order plus this
/// revision's new keys, exactly matching the pre-WMP publication algorithm.
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

    private FrozenDictionary<TKey, TValue> CanonicalView
    {
        get
        {
            if (_canonicalView is not null)
                return _canonicalView;
            var pending = new Stack<PersistentRevisionDictionary<TKey, TValue>>();
            for (var cursor = this; cursor is not null && cursor._canonicalView is null;
                 cursor = cursor._canonicalParent)
                pending.Push(cursor);
            while (pending.TryPop(out var revision))
                revision._canonicalView = revision.CanonicalEntries().ToFrozenDictionary(revision._storage.KeyComparer);
            return _canonicalView!;
        }
    }

    private IEnumerable<KeyValuePair<TKey, TValue>> CanonicalEntries()
    {
        if (_canonicalParent?._canonicalView is { } parentView)
            foreach (var key in parentView.Keys)
                yield return new KeyValuePair<TKey, TValue>(key, _storage[key]);
        foreach (var key in _addedKeys)
            yield return new KeyValuePair<TKey, TValue>(key, _storage[key]);
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
/// a consumer that enumerates the public EvidenceBasis.
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

    private FrozenSet<T> CanonicalView
    {
        get
        {
            if (_canonicalView is not null)
                return _canonicalView;
            var pending = new Stack<PersistentRevisionSet<T>>();
            for (var cursor = this; cursor is not null && cursor._canonicalView is null;
                 cursor = cursor._canonicalParent)
                pending.Push(cursor);
            while (pending.TryPop(out var revision))
                revision._canonicalView = revision.CanonicalItems().ToFrozenSet(revision._storage.KeyComparer);
            return _canonicalView!;
        }
    }

    private IEnumerable<T> CanonicalItems()
    {
        if (_canonicalParent?._canonicalView is { } parentView)
            foreach (var item in parentView)
                yield return item;
        foreach (var item in _addedItems)
            yield return item;
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
