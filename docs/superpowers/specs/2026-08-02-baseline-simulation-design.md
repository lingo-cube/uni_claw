# 离线基线驱动仿真测试设计

> 状态: draft | 日期: 2026-08-02

## 1. 动机

线上 emulator run 产生的 trace span 数据可以反向驱动仿真测试，形成数据闭环：

```
线上 run → trace spans → BaselineBuilder → baseline.jsonl
                                              │
                                              ▼
                                  BaselineSimulationProfile
                                              │
                                              ▼
                                  FsmSimulationHarness
                                              │
                                              ▼
                                  CI 回归测试（断言行为匹配基线）
```

## 2. 数据流

### 2.1 写入：BaselineBuilder

每次 run 结束，从 trace span 提取基线数据并追加到 `artifacts/baselines/<scenarioId>.jsonl`：

```json
{"scenarioId":"enumerate-settings-safely","timestamp":"2026-08-02T11:00:00Z","itemsObserved":18,"itemsVisited":14,"itemsSkipped":2,"stepsUsed":87,"scrollCount":8,"endOfListDetected":true,"success":true,"aiLatencyP50":4500,"aiLatencyP95":8200}
{"scenarioId":"enumerate-settings-safely","timestamp":"2026-08-02T12:30:00Z","itemsObserved":16,"itemsVisited":13,"itemsSkipped":1,"stepsUsed":92,"scrollCount":9,"endOfListDetected":true,"success":true,"aiLatencyP50":4300,"aiLatencyP95":7900}
```

### 2.2 读取：BaselineSimulationProfile

```csharp
public sealed class BaselineSimulationProfile
{
    public static BaselineSimulationProfile? Load(string scenarioId)
    {
        var path = $"artifacts/baselines/{scenarioId}.jsonl";
        if (!File.Exists(path)) return null;

        var records = File.ReadLines(path)
            .Select(JsonSerializer.Deserialize<BaselineRecord>)
            .Where(r => r?.Success == true)  // 只用成功的 run
            .ToList();

        if (records.Count < 10) return null;  // 不够

        var visited  = records.Select(r => r.ItemsVisited).OrderBy(x => x).ToList();
        var steps    = records.Select(r => r.StepsUsed).OrderBy(x => x).ToList();
        var scrolls  = records.Select(r => r.ScrollCount).OrderBy(x => x).ToList();
        var observed = records.Select(r => r.ItemsObserved).OrderBy(x => x).ToList();
        var aiP95    = records.Select(r => r.AiLatencyP95).OrderBy(x => x).ToList();

        return new(
            ExpectedItemsObserved: Percentile(observed, 50),
            ExpectedItemsVisited:  Percentile(visited, 50),
            MaxSteps:              Percentile(steps, 95),
            MaxScrolls:            Percentile(scrolls, 95),
            AiLatencyP95:          Percentile(aiP95, 95),
            EndOfListExpected:     records.Count(r => r.EndOfListDetected) >= records.Count * 0.7,
            SampleCount:           records.Count
        );
    }

    private static int Percentile(List<int> sorted, int p)
    {
        var idx = (int)Math.Ceiling(sorted.Count * p / 100.0) - 1;
        return sorted[Math.Clamp(idx, 0, sorted.Count - 1)];
    }

    public int ExpectedItemsObserved { get; init; }
    public int ExpectedItemsVisited { get; init; }
    public int MaxSteps { get; init; }
    public int MaxScrolls { get; init; }
    public double AiLatencyP95 { get; init; }
    public bool EndOfListExpected { get; init; }
    public int SampleCount { get; init; }
}

public sealed record BaselineRecord(
    string ScenarioId,
    DateTimeOffset Timestamp,
    int ItemsObserved,
    int ItemsVisited,
    int ItemsSkipped,
    int StepsUsed,
    int ScrollCount,
    bool EndOfListDetected,
    bool Success,
    double AiLatencyP50,
    double AiLatencyP95);
```

## 3. 仿真用例生成

### 3.1 输入注入

```
Profile → FsmSimulationHarness.ForEnumerate(profile)
  │
  ├── MockPageAnalyzer
  │     AnalyzeCurrentPageAsync() 返回 profile.ExpectedItemsObserved 个 MenuItem
  │     (用 CallbackPageAnalyzer 逐步返回，模拟滚动发现新条目)
  │
  ├── MockScreenState
  │     HasScroll()     → true (前 profile.MaxScrolls 次)
  │     HasScroll()     → false (之后)
  │     IsEndOfList()   → profile.EndOfListExpected (profile.MaxScrolls 次后)
  │
  ├── MockActionExecutor
  │     TapAsync()   → 记录 entry.visited 计数
  │     SwipeAsync() → 记录 scroll 计数
  │     PressBackAsync() → 记录
  │
  └── CallbackPageAnalyzer
        每次 call 返回不同的 PageAnalysis（模拟翻页）
```

### 3.2 断言矩阵

| 断言 | 条件 | 来源 |
|------|------|------|
| `harness.VisitedCount >= profile.ExpectedItemsVisited * 0.8` | 至少访问了基线的 80% | p50_items |
| `harness.StepsCount <= profile.MaxSteps * 1.2` | 步数不超 p95 的 120% | p95_steps |
| `harness.ScrollCount <= profile.MaxScrolls` | 滚动不超 p95 | p95_scrolls |
| `harness.EndOfListReached == profile.EndOfListExpected` | 列表到底行为一致 | endOfList |
| `harness.AiCalls.All(c => c.LatencyMs <= profile.AiLatencyP95 * 3)` | 无极端耗时 | p95_ai_ms |
| `harness.VisitedCount / (double)harness.ObservedCount >= 0.7` | 效率合理 | derived |
| `harness.SkippedCount <= profile.SampleCount * 2` | 不过度跳过 | derived |

### 3.3 循环检测

```
连续 5 步 visited 不变 → 卡住 → 测试失败
同一页面 visited 同一 item > 2 次 → 循环 → 测试失败
steps > MaxSteps * 3 → 超限 → 测试失败
```

### 3.4 测试代码

```csharp
[Theory]
[MemberData(nameof(AllProfiles))]
public void Simulation_MatchesBaseline(BaselineSimulationProfile profile)
{
    var harness = FsmSimulationHarness.ForEnumerate(profile);

    var result = harness.RunToCompletion();

    // 断言组
    Assert.True(result.Succeeded, $"Run failed: {result.Reason}");
    Assert.True(harness.VisitedCount >= profile.ExpectedItemsVisited * 0.8,
        $"Visited {harness.VisitedCount} < {profile.ExpectedItemsVisited * 0.8}");
    Assert.True(harness.StepsCount <= profile.MaxSteps * 1.2,
        $"Steps {harness.StepsCount} > {profile.MaxSteps * 1.2}");
}

public static IEnumerable<object[]> AllProfiles()
{
    var dir = "artifacts/baselines";
    if (!Directory.Exists(dir)) yield break;
    foreach (var file in Directory.GetFiles(dir, "*.jsonl"))
    {
        var id = Path.GetFileNameWithoutExtension(file);
        var profile = BaselineSimulationProfile.Load(id);
        if (profile is not null)
            yield return new object[] { profile };
    }
}
```

## 4. 数据闭环

```
Run 1-9:   baseline < 10 条 → 测试 SKIP（数据不够）
Run 10:    baseline = 10 条 → 首次生成 Profile → CI 启用回归测试
Run 11-N:  每次追加 → Profile 自动更新 → 阈值渐进调整
          如果新 run 行为偏离基线 > 20% → 告警（可能引擎改动引入回归）
```

## 5. 文件清单

| 文件 | 内容 |
|------|------|
| `test/Core.Tests/Simulation/BaselineRecord.cs` | JSON 反序列化 record |
| `test/Core.Tests/Simulation/BaselineSimulationProfile.cs` | 基线 → 仿真参数转换 + 百分位计算 |
| `test/Core.Tests/Simulation/BaselineRegressionTests.cs` | 数据驱动回归测试 |
| `src/Host/Analysis/BaselineBuilder.cs` | 线上 run → 提取基线数据 → 写 JSONL |

## 6. 和 Trace Span 的关系

```
Trace Span (数据源)
  ├── entry.observed  → BaselineRecord.ItemsObserved
  ├── entry.visited   → BaselineRecord.ItemsVisited
  ├── entry.skipped   → BaselineRecord.ItemsSkipped
  ├── engine.step     → BaselineRecord.StepsUsed
  ├── action.scroll   → BaselineRecord.ScrollCount
  ├── entry.generate  → BaselineRecord.EndOfListDetected
  └── ai.call         → BaselineRecord.AiLatencyP50/P95

BaselineBuilder 消费 ITraceQuery.GetSpansByType()
  → 聚合 → 写 JSONL
BaselineSimulationProfile 消费 JSONL
  → Profile → FsmSimulationHarness 参数化
```
