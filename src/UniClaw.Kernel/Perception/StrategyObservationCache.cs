using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using UniClaw.Kernel.Diagnostics;

namespace UniClaw.Kernel.Perception;

/// <summary>
/// StrategyObservationCache — FastPerception 所有权内部的有界确定性计算
/// 复用缓存（FCR-001，internal 实现细节，不扩大任何公共接口）。
/// 键 = (StrategyIdentity, StrategyVersion, ArtifactId)（ordinal 比较）；
/// 值 = 插入时防御性拷贝的 ArtifactObservation 快照——**唯一允许的缓存物**
/// （proposal / evidence / belief / revision 等任何下游产物禁入，FCR-001
/// 边界 2/4）。
/// 契约：
/// <list type="bullet">
/// <item>有界：容量显式（&lt;1 fail-closed）；满载插入前按 LRU 访问序淘汰
/// 尾部（插入或命中均刷新 recency；驱动序列确定 ⇒ 淘汰行为确定）。</item>
/// <item>single-flight：同完整键并发时只执行一次 compute（后到者在锁外
/// 等待 holder）；成功共享同一结果快照，失败经 ExceptionDispatchInfo 同型
/// 重抛给全部等待者。</item>
/// <item>fail-safe：compute 抛错 / 取消 ⇒ 零缓存写入、inflight 立即移除，
/// 后续调用重算（失败不污染缓存）。</item>
/// <item>隔离：命中与计算路径每次返回新数组拷贝——调用方（含对 strategy
/// 原始返回 list 的事后改动）无法改写内部快照。</item>
/// <item>生命周期 = 持有它的 FastPerception 实例（组合根拥有），无静态
/// 全局状态。</item>
/// </list>
/// </summary>
internal sealed class StrategyObservationCache
{
    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly Dictionary<Key, LinkedListNode<Entry>> _nodes = new();
    private readonly LinkedList<Entry> _lru = new(); // head = most recently used
    private readonly Dictionary<Key, InFlight> _inFlight = new();

    internal StrategyObservationCache(int capacity)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity), "缓存容量必须 ≥ 1");
        _capacity = capacity;
    }

    private readonly record struct Key(string StrategyIdentity, string StrategyVersion, string ArtifactId);

    private sealed record Entry(Key CacheKey, ArtifactObservation[] Observations);

    private sealed class InFlight
    {
        public readonly object Signal = new();
        public bool Done;
        public ArtifactObservation[]? Result;
        public Exception? Error;
    }

    /// <summary>
    /// 带复用的观察计算：命中 → 返回快照拷贝（不执行 compute）；未命中 →
    /// single-flight 执行一次 compute 并缓存。metrics 计数（lookup / hit /
    /// miss / eviction / single-flight wait）在本处 additive 记录。
    /// </summary>
    internal IReadOnlyList<ArtifactObservation> ObserveWithReuse(
        string strategyIdentity,
        string strategyVersion,
        string artifactId,
        Func<IReadOnlyList<ArtifactObservation>> compute,
        RuntimeStageMetrics? metrics)
    {
        var key = new Key(strategyIdentity, strategyVersion, artifactId);
        metrics?.CountCacheLookup();

        InFlight? waiting = null;
        InFlight? elected = null;
        lock (_gate)
        {
            if (_nodes.TryGetValue(key, out var node))
            {
                _lru.Remove(node);
                _lru.AddFirst(node); // 命中刷新 recency
                metrics?.CountCacheHit();
                return (ArtifactObservation[])node.Value.Observations.Clone();
            }

            metrics?.CountCacheMiss();
            if (_inFlight.TryGetValue(key, out var existing))
            {
                waiting = existing;
            }
            else
            {
                elected = new InFlight();
                _inFlight[key] = elected;
            }
        }

        if (waiting is not null)
        {
            metrics?.CountCacheSingleFlightWait();
            lock (waiting.Signal)
            {
                while (!waiting.Done)
                    Monitor.Wait(waiting.Signal);
            }
            if (waiting.Error is not null)
                ExceptionDispatchInfo.Capture(waiting.Error).Throw();
            return (ArtifactObservation[])waiting.Result!.Clone();
        }

        // elected 调用方：compute 在锁外执行（不得持 _gate 做 strategy 计算）
        ArtifactObservation[] snapshot;
        try
        {
            snapshot = compute().ToArray(); // 防御性拷贝 = 冻结为不可变快照
        }
        catch (Exception error)
        {
            CompleteFlight(key, elected!, result: null, error);
            throw;
        }

        lock (_gate)
        {
            if (_nodes.Count >= _capacity && !_nodes.ContainsKey(key))
            {
                var tail = _lru.Last!;
                _lru.RemoveLast();
                _nodes.Remove(tail.Value.CacheKey);
                metrics?.CountCacheEviction();
            }
            var node = new LinkedListNode<Entry>(new Entry(key, snapshot));
            _lru.AddFirst(node);
            _nodes[key] = node;
        }
        CompleteFlight(key, elected!, snapshot, error: null);
        return (ArtifactObservation[])snapshot.Clone();
    }

    private void CompleteFlight(Key key, InFlight flight, ArtifactObservation[]? result, Exception? error)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_inFlight.TryGetValue(key, out var registered) ? registered : null, flight))
                _inFlight.Remove(key);
        }
        lock (flight.Signal)
        {
            flight.Done = true;
            flight.Result = result;
            flight.Error = error;
            Monitor.PulseAll(flight.Signal);
        }
    }
}
