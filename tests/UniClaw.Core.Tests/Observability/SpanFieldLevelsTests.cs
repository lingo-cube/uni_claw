using System.Reflection;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using Xunit;

namespace UniClaw.Core.Tests.Observability;

/// <summary>
/// SpanFieldLevelsTests — TraceLevel 分级记录字段测试 (trace-parent-linkage M2, tasks 3.1/3.4/3.5)。
/// 验证 AC6: 缺省（Detailed）记录的字段集与 change 前全量记录一致；Basic 级别记录核心字段、
/// 不记录 Detailed+ 字段；None 级别属性为空；profile==null 时任何 level 都全量；
/// 以及 TraceSpanFields 对 TraceFields 48 键的完整覆盖（防 profile 遗漏键）。
/// </summary>
public class SpanFieldLevelsTests
{
    // ── 缺省兼容（AC6 根基）──────────────────────────────

    [Fact(DisplayName = "分级: 缺省（不传 level）记录全部键，与不传 profile 行为一致")]
    public async Task DefaultLevel_RecordsAllKeys_LikeNoProfile()
    {
        var (recorder, service) = NewTrace();

        TraceSpanScope scope;
        await using (scope = await recorder.BeginSpanAsync(
                         SpanTypes.AiCall,
                         "ai",
                         attributes: StartAttributes(),
                         profile: TraceSpanFields.AiCall))
        {
            await scope.End("ok", EndAttributes());
        }

        var span = service.GetSpan(scope.SpanId);
        Assert.NotNull(span);
        Assert.Equal(AllAiCallKeys, span.Attributes!.Keys.OrderBy(k => k).ToArray());
    }

    [Fact(DisplayName = "分级: 显式 Detailed 与缺省同样全量")]
    public async Task DetailedLevel_RecordsAllKeys()
    {
        var (recorder, service) = NewTrace();

        TraceSpanScope scope;
        await using (scope = await recorder.BeginSpanAsync(
                         SpanTypes.AiCall,
                         "ai",
                         attributes: StartAttributes(),
                         profile: TraceSpanFields.AiCall,
                         level: TraceLevel.Detailed))
        {
            await scope.End("ok", EndAttributes());
        }

        var span = service.GetSpan(scope.SpanId);
        Assert.NotNull(span);
        Assert.Equal(AllAiCallKeys, span.Attributes!.Keys.OrderBy(k => k).ToArray());
    }

    [Fact(DisplayName = "分级: profile 为 null 时任何 level 都全量记录")]
    public async Task NullProfile_AnyLevel_RecordsAllKeys()
    {
        foreach (var level in new[] { TraceLevel.None, TraceLevel.Basic, TraceLevel.Detailed, TraceLevel.Full })
        {
            var (recorder, service) = NewTrace();

            TraceSpanScope scope;
            await using (scope = await recorder.BeginSpanAsync(
                             SpanTypes.AiCall,
                             "ai",
                             attributes: StartAttributes(),
                             profile: null,
                             level: level))
            {
                await scope.End("ok", EndAttributes());
            }

            var span = service.GetSpan(scope.SpanId);
            Assert.NotNull(span);
            Assert.Equal(AllAiCallKeys, span.Attributes!.Keys.OrderBy(k => k).ToArray());
        }
    }

    // ── Basic 级别（核心字段）─────────────────────────────

    [Fact(DisplayName = "分级: Basic 级别仅记录 profile.Basic 核心键（开始与结束属性均过滤）")]
    public async Task BasicLevel_RecordsOnlyBasicKeys()
    {
        var (recorder, service) = NewTrace();

        TraceSpanScope scope;
        await using (scope = await recorder.BeginSpanAsync(
                         SpanTypes.AiCall,
                         "ai",
                         attributes: StartAttributes(),
                         profile: TraceSpanFields.AiCall,
                         level: TraceLevel.Basic))
        {
            await scope.End("ok", EndAttributes());
        }

        var span = service.GetSpan(scope.SpanId);
        Assert.NotNull(span);
        Assert.Equal(
            new[] { TraceFields.AiCapability, TraceFields.AiMode, TraceFields.AiSuccess },
            span.Attributes!.Keys.OrderBy(k => k).ToArray());
    }

    [Fact(DisplayName = "分级: action.wait profile — Basic 级别只留 type/result，wait_ms 过滤")]
    public async Task BasicLevel_ActionWait_OnlyTypeAndResult()
    {
        var (recorder, service) = NewTrace();

        TraceSpanScope scope;
        await using (scope = await recorder.BeginSpanAsync(
                         SpanTypes.ActionWait,
                         "wait",
                         attributes: new Dictionary<string, object> { [TraceFields.ActionType] = "wait" },
                         profile: TraceSpanFields.ActionWait,
                         level: TraceLevel.Basic))
        {
            await scope.End(
                "ok",
                new Dictionary<string, object>
                {
                    [TraceFields.ActionResult] = true,
                    [TraceFields.ActionWaitMs] = 100,
                });
        }

        var span = service.GetSpan(scope.SpanId);
        Assert.NotNull(span);
        Assert.Equal(
            new[] { TraceFields.ActionResult, TraceFields.ActionType },
            span.Attributes!.Keys.OrderBy(k => k).ToArray());
    }

    [Fact(DisplayName = "分级: Basic 级别下空 Basic 集的 profile（Poll）属性为空")]
    public async Task BasicLevel_EmptyBasicProfile_AttributesEmpty()
    {
        var (recorder, service) = NewTrace();

        TraceSpanScope scope;
        await using (scope = await recorder.BeginSpanAsync(
                         SpanTypes.AnalyzeCompletion,
                         "completion poll",
                         attributes: new Dictionary<string, object>
                         {
                             [TraceFields.PollVerdict] = "done",
                             [TraceFields.PollConfidence] = 0.95,
                         },
                         profile: TraceSpanFields.Poll,
                         level: TraceLevel.Basic))
        {
            await scope.End(
                "ok",
                new Dictionary<string, object>
                {
                    [TraceFields.PollAction] = "cancel",
                    [TraceFields.PollEscalated] = false,
                });
        }

        var span = service.GetSpan(scope.SpanId);
        Assert.NotNull(span);
        Assert.True(span.Attributes is null || span.Attributes.Count == 0,
            "空 Basic 集的 profile 在 Basic 级别下属性应为空（存储合并后坍缩为 null）");
    }

    // ── None 级别（属性为空，span 照常记录）───────────────

    [Fact(DisplayName = "分级: None 级别 span 照常记录，属性为空、status 保留")]
    public async Task NoneLevel_EmptyAttributes_StatusPreserved()
    {
        var (recorder, service) = NewTrace();

        TraceSpanScope scope;
        await using (scope = await recorder.BeginSpanAsync(
                         SpanTypes.AiCall,
                         "ai",
                         attributes: StartAttributes(),
                         profile: TraceSpanFields.AiCall,
                         level: TraceLevel.None))
        {
            await scope.End("error", EndAttributes());
        }

        var span = service.GetSpan(scope.SpanId);
        Assert.NotNull(span);
        Assert.Equal("error", span.Status);
        Assert.True(span.Attributes is null || span.Attributes.Count == 0,
            "None 级别下 span 属性应为空（存储合并后坍缩为 null）");
    }

    // ── RecordEventAsync 同样过滤 ─────────────────────────

    [Fact(DisplayName = "分级: RecordEventAsync 在 Basic 级别过滤 Extended 键")]
    public async Task RecordEventAsync_BasicLevel_FiltersExtendedKeys()
    {
        var (recorder, service) = NewTrace();

        await recorder.RecordEventAsync(
            SpanTypes.EntryVisited,
            parentSpanId: null,
            new Dictionary<string, object>
            {
                [TraceFields.EntryName] = "wifi",
                [TraceFields.EntryNodeId] = "node-1",
                [TraceFields.EntryStep] = 3,
                [TraceFields.EntryDepth] = 2,
            },
            profile: TraceSpanFields.EntryVisited,
            level: TraceLevel.Basic);

        var span = Assert.Single(service.GetSpansByType(SpanTypes.EntryVisited));
        Assert.Equal(TraceFields.EntryName, Assert.Single(span.Attributes).Key);
    }

    [Fact(DisplayName = "分级: RecordEventAsync 在 Detailed 缺省下全量")]
    public async Task RecordEventAsync_DefaultLevel_RecordsAllKeys()
    {
        var (recorder, service) = NewTrace();

        await recorder.RecordEventAsync(
            SpanTypes.EntryVisited,
            parentSpanId: null,
            new Dictionary<string, object>
            {
                [TraceFields.EntryName] = "wifi",
                [TraceFields.EntryNodeId] = "node-1",
                [TraceFields.EntryStep] = 3,
                [TraceFields.EntryDepth] = 2,
            },
            profile: TraceSpanFields.EntryVisited);

        var span = Assert.Single(service.GetSpansByType(SpanTypes.EntryVisited));
        Assert.Equal(4, span.Attributes!.Count);
    }

    // ── TraceSpanFields 完整性（48 键全覆盖）──────────────

    [Fact(DisplayName = "分级: TraceSpanFields 覆盖 TraceFields 全部 48 键（Basic ∪ Extended）")]
    public void TraceSpanFields_CoverAllTraceFieldsKeys()
    {
        var allKeys = typeof(TraceFields)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .ToArray();

        Assert.Equal(48, allKeys.Length);

        var profiles = EnumerateProfiles();
        var covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var profile in profiles)
        {
            foreach (var key in profile.Basic) covered.Add(key);
            foreach (var key in profile.Extended) covered.Add(key);
        }

        var missing = allKeys.Where(k => !covered.Contains(k)).OrderBy(k => k).ToArray();
        Assert.Empty(missing);
    }

    [Fact(DisplayName = "分级: profile 键全部为 TraceFields 目录成员，且 Basic ∩ Extended 为空")]
    public void Profiles_KeysAreCatalogMembers_NoBasicExtendedOverlap()
    {
        var profiles = EnumerateProfiles();
        Assert.NotEmpty(profiles);

        foreach (var profile in profiles)
        {
            foreach (var key in profile.Basic.Concat(profile.Extended))
            {
                Assert.True(TraceFields.IsKnown(key), $"profile 键 '{key}' 不在 TraceFields 目录中");
            }
            Assert.Empty(profile.Basic.Intersect(profile.Extended));
        }
    }

    // ── helpers ──────────────────────────────────────────

    private static Dictionary<string, object> StartAttributes() => new()
    {
        [TraceFields.AiCapability] = "analyze_visual",
        [TraceFields.AiMode] = "vision",
    };

    private static Dictionary<string, object> EndAttributes() => new()
    {
        [TraceFields.AiProviderId] = "sensenova",
        [TraceFields.AiModel] = "flash-lite",
        [TraceFields.AiMode] = "vision",
        [TraceFields.AiTokens] = 1234,
        [TraceFields.AiSuccess] = true,
        [TraceFields.AiLatencyMs] = 88L,
    };

    /// <summary>ai.call 全量属性键（start 2 + end 5 去重 = 7）。</summary>
    private static readonly string[] AllAiCallKeys =
    [
        TraceFields.AiCapability, TraceFields.AiLatencyMs, TraceFields.AiMode,
        TraceFields.AiModel, TraceFields.AiProviderId, TraceFields.AiSuccess, TraceFields.AiTokens,
    ];

    private static IReadOnlyList<SpanFieldProfile> EnumerateProfiles() =>
        typeof(TraceSpanFields)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(SpanFieldProfile))
            .Select(f => (SpanFieldProfile)f.GetValue(null)!)
            .ToArray();

    private static (InMemoryTraceRecorder Recorder, InMemoryTraceService Service) NewTrace()
    {
        var storage = new InMemoryTraceStorage();
        return (new InMemoryTraceRecorder(storage), new InMemoryTraceService(storage));
    }
}
