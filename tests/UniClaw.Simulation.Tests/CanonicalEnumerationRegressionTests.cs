using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// UAP-001 tracer 发现并修复的产品缺陷回归（修复授权：Human 2026-09-14，
/// 见 changes/UAP-001）：PersistentRevisionDictionary/Set.BuildCanonicalView
/// 在「中途缓存视图 + 之后仅 SetItem（无新增键）revision」链上曾以 null
/// 为重建基，canonical 枚举静默丢失缓存祖先已建立的键并把错误视图永久
/// 缓存（Count/TryGetValue 走 storage 不受影响，故既有测试未触发）。本
/// 回归固定修复后行为：枚举始终与 storage 一致。
/// </summary>
public sealed class CanonicalEnumerationRegressionTests
{
    private readonly ITestOutputHelper _output;
    public CanonicalEnumerationRegressionTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void CachedMidChainView_ThenSetItemOnlyRevisions_EnumerationKeepsAncestorKeys()
    {
        var world = new WorldModel(
            new HashSet<string>(StringComparer.Ordinal) { "live.frame", "s.early", "s.late" });

        var t = new DateTimeOffset(2026, 9, 14, 9, 0, 0, TimeSpan.Zero);
        var n = 0;
        void Put(string subject, string value, string scope)
        {
            var id = "ev-" + (n++).ToString().PadLeft(64, '0');
            world.Reconcile(new EvidenceRecord(
                id, new ObservationClaim(subject, value),
                IngressKind.Observation, ObservationContext.External,
                new Provenance("p", t.AddSeconds(n), scope, new[] { "l" })),
                new RelevanceJudgment(id, true, "subject-in-relevance-scope"));
        }

        // 早期键建立（adds）→ 中途枚举（缓存中间 canonical view）→
        // 仅 SetItem 的再观察（CLE-001 Revise：同 producer 异 scope）
        Put("live.frame", "v1", "s:a");
        Put("s.early", "on", "s:a");
        _ = world.Current!.WorldState.Count();
        Put("s.early", "off", "s:b");
        Put("s.late", "x", "s:c");

        var final = world.Current!;
        var keys = final.WorldState.Select(kv => kv.Key).OrderBy(k => k).ToList();
        _output.WriteLine($"count={final.WorldState.Count} enum=[{string.Join("|", keys)}]");

        Assert.Equal(final.WorldState.Count, keys.Count);
        Assert.Contains("live.frame", keys);
        Assert.Contains("s.early", keys);
        Assert.Contains("s.late", keys);
        Assert.Equal("off", final.WorldState["s.early"].Value); // Revise 值正确

        // basis 集合（PersistentRevisionSet 同缺陷同修）枚举一致
        Assert.Equal(final.EvidenceBasis.Count, final.EvidenceBasis.Count());
    }
}
