# AI 编译期优化介入点 — 设计文档

> 状态：设计提案
> 日期：2026-08-01
> 作者：Fran + Claude

## 1. 动机

当前链路：

```
场景描述 → AI IntentExtractor → IntentSlots → PlanCompiler → TraversalPlan → 仿真/执行
```

`IntentExtractor` 推理了 5 个维度（scope / element_handling / navigation / restore / completion），但 `PlanCompiler` 是纯确定性的 —— 它用固定的 `TemplateSets` 和 `MatchConditions` 生成 DynamicMatch 根节点，没有任何 AI 推理介入。

这意味着 Plan 编译出来后，引擎只能靠 DynamicMatch 逐页扫描。AI 的常识推理能力（"About phone 通常在 Settings 列表底部"、"Battery 页面没有可导航子菜单"）和历史数据（"上次 About phone 在坐标 (0.5, 0.44)"）完全没用上。

**目标**：在编译期让 AI 往 TraversalPlan 注入优化标记，运行时 FSM 不感知、不修改。

## 2. 设计原则

1. **编译期注入 / 运行时 FSM 不变**：AI 优化在 `ScenarioPlanCompiler.Compile()` 中完成，注入到 `TraversalPlan` 结构里。引擎 FSM 照常执行，只是遇到的 plan 更"丰富"了
2. **AI 输出与 Plan 结构解耦**：AI 返回意图级 DTO（`OptimizationHints`），由确定性 `PlanOptimizationInjector` 负责转换为 Plan 结构
3. **优雅降级**：AI 不可用或失败 → plan 回退到纯确定性版本（与今天完全一致）
4. **可观测**：所有注入标记记录在 `TraversalPlan.Meta` 和 `TraversalNode.Meta` 中

## 3. 核心技巧：Static + dyn_fallback

引擎原生支持 `ChildrenStrategy.Static` + `StaticNodes`。一个 `ChildrenStrategy` 不能同时 Static 和 DynamicMatch，但可以在树的不同层级混用。

我们把 root 从纯 `DynamicMatch` 改为 `Static`：

```
root (Screen, NoAction, ChildrenStrategy.Static)
├── ai_about_phone (Container, Click Target(Coordinate 0.5,0.44), Static.None)
├── ai_wifi         (Container, Click Target(Text "Wi-Fi"), Static.None)
└── dyn_fallback    (Container, NoAction, ChildrenStrategy = 原始 DynamicMatch 规则)
```

引擎按序处理 Static children：
1. 先 click "About phone" 的预解析坐标 → 成功 → TargetFound 早停 ✅
2. 如果坐标失效（页面变了），Click 失败 → ErrorHandling → 跳过该 child
3. 所有 Static children 访问完后，进入 dyn_fallback → DynamicMatch 兜底遍历

## 4. 新增组件

### 4.1 IPlanOptimizationAdvisor（Core）

```csharp
// src/UniClaw.Core/UniBrain/IPlanOptimizationAdvisor.cs

public interface IPlanOptimizationAdvisor
{
    Task<OptimizationHints> SuggestAsync(
        string description,       // 场景的自然语言描述
        string targetApp,         // 目标应用包名
        string? target,           // 目标项标签
        string scope,             // "full" | "target_only"（IntentSlots 已解析）
        int? maxDepth,            // 最大深度
        string? entryPage,        // 入口页面
        CancellationToken ct = default);
}

/// <summary>AI 建议的预解析静态节点。</summary>
public sealed record class SuggestedStaticNode(
    string NodeId,                          // AI 指定的节点 ID
    string Name,                            // 显示名
    string TargetBy,                        // "text" | "coordinate"
    string? Text,                           // text target 值
    double? X, double? Y,                   // coordinate target 值（归一化 0..1）
    IReadOnlyList<SuggestedStaticNode>? Children  // 嵌套子路径
);

/// <summary>AI 输出的优化提示（意图级 DTO，与 Plan 结构解耦）。</summary>
public sealed record class OptimizationHints(
    IReadOnlyList<SuggestedStaticNode> StaticNodes,
    IReadOnlyList<string> SkipEntries,
    string? Reasoning
);
```

### 4.2 PlanOptimizationAdvisor（Core）

实现 mirror `IntentExtractor`：

```
PromptTemplateRegistry.SuggestOptimizations
  → template.Resolve(variables)
  → _modelProvider.CompleteTextAsync(request)
  → StripCodeFences(raw)
  → JsonSerializer.Deserialize&lt;OptimizationHints&gt;
  → 词表校验（TargetBy ∈ {text, coordinate}、坐标 ∈ [0,1]、node_id regex）
  → 返回 OptimizationHints
```

### 4.3 PlanOptimizationInjector（Core，确定性变换）

```
TraversalPlan × OptimizationHints → TraversalPlan
```

三个注入动作：

**A. Static 路径注入**

1. 遍历 `hints.StaticNodes`，递归构造 `TraversalNode`：
   - `NodeType = Container`
   - `Operation = Click, Target(By=Text|Coordinate, Value=text|Coordinate(x,y))`
   - `ChildrenStrategy = Static`（有 children）或 `None`（叶子节点）
   - `ErrorPolicy = Retry, MaxRetries=1`
2. 注册到 `plan.StaticNodes`，key = `ai_{nodeId}`
3. 创建 `dyn_fallback` 节点：`Container`, `NoAction`, `ChildrenStrategy = 原 root 的 ChildrenStrategy`（承载完整 DynamicMatch 规则）
4. 注册 `dyn_fallback` 到 `StaticNodes`
5. Root 改写：`ChildrenStrategy = Static(StaticChildren = [ai_* ids..., "dyn_fallback"])`

**B. Skip-rule 注入**

在 `dyn_fallback` 的 `DynamicRules` 字典前插入 Skip 规则：

```csharp
foreach (var entry in hints.SkipEntries)
{
    rules[$"ai_skip_{Sanitize(entry)}"] = new DynamicRule(
        RuleId: $"ai_skip_{Sanitize(entry)}",
        MatchCondition: new MatchCondition(TextPattern: entry, TextMatchMode: Exact),
        ChildTemplate: "",
        Action: MatchAction.Skip);
}
```

`DynamicMatcher.MatchAll` 按 Dictionary 插入顺序 first-match-wins，所以 Skip 规则会优先于 `menu_container` 等模板规则命中。

**C. Meta 标注**

- Plan 级：`Meta["aiOptimization"]` = `{version, appliedAt, advisorModel, source, injectedNodeIds, reasoning}`
- 节点级：`Meta["ai_injected"] = true`、`Meta["ai_confidence"] = <double>`、`Meta["ai_source"] = "common_sense"`

空 `hints`（StaticNodes 为空且 SkipEntries 为空）→ Injector 原样返回 plan（no-op）。

## 5. AI Prompt 模板设计

### System Prompt 要点

- 说明 Android Settings 应用的典型结构：主列表是可滚动的 menu_item 行
- 常见直接条目："About phone"、"Battery"、"Display"、"Storage"
- 常见二级条目："Internal Storage"（在 Storage 下）、"HomeNetwork"（在 Wi-Fi 下）
- 输出约束：**恰好一个 JSON 对象**，不要 markdown fence；node_id 必须是 `[a-z0-9_]+`；坐标归一化 [0,1]；叶子节点的 name 应当等于 target label；skip_entries 中不能包含 target label
- 优先使用 text target；只在坐标高度确定时才用 coordinate

### User Prompt 变量

```
Scenario: {description}
Target app: {target_app}
Target item: {target}
Scope: {scope}
Max depth: {depth}
Entry page: {entry}

Suggest static paths and skip entries. Respond ONLY with JSON.
```

### Property Name 对齐

Prompt 中声明 `static_nodes` / `skip_entries` / `reasoning`，对齐 `OptimizationHints` 的 JSON property name（camelCase）。

## 6. 编译期集成点

`ScenarioPlanCompiler.Compile()` 在 PlanCompiler 之后、Meta stamp 之前接入：

```csharp
public TraversalPlan Compile(ScenarioSnapshot snapshot)
{
    // ... existing: IntentExtractor → base plan ...
    var plan = new PlanCompiler().Compile(slots);

    // === NEW: AI optimization injection ===
    if (_optimizationAdvisor is not null)
    {
        try
        {
            var hints = _optimizationAdvisor.SuggestAsync(
                scenario.Description,
                scenario.AppPackage,
                scenario.Target?.Label,
                slots.Scope,
                scenario.Boundaries.MaxDepth,
                scenario.ResetProcedure.ExpectedPageIdentity)
                .GetAwaiter().GetResult();

            plan = PlanOptimizationInjector.Inject(plan, hints);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.WriteLine($"[ScenarioPlanCompiler] AI optimization failed, "
                + $"using unoptimized plan: {ex.Message}");
        }
    }

    // ... existing: target-rule injection, CompletionPolicy, Meta stamp ...
}
```

## 7. Host 装配

`HostCommands.CreatePlanOptimizationAdvisor()` mirror `CreateIntentExtractor()`：

```csharp
private static IPlanOptimizationAdvisor? CreatePlanOptimizationAdvisor()
{
    var apiKey = LoadSensenovaApiKey();
    if (string.IsNullOrWhiteSpace(apiKey)) return null;

    var model = Environment.GetEnvironmentVariable("SENSENOVA_MODEL") ?? "deepseek-v4-flash";
    var baseUrl = Environment.GetEnvironmentVariable("SENSENOVA_BASE_URL") ?? "https://token.sensenova.cn";
    var config = new OpenAiCompatibleProviderConfig(apiKey, model, baseUrl);
    var provider = new OpenAiCompatibleVisionProvider(new HttpClient(), config);
    return new PlanOptimizationAdvisor(provider);
}

// RunScenarioAsync 中:
var compiler = new ScenarioPlanCompiler(
    CreateIntentExtractor(),
    CreatePlanOptimizationAdvisor());
```

## 8. FSM 兼容性证明

| 引擎能力 | 原生支持？ | Optimized Plan 如何利用 |
|---|---|---|
| `ChildrenStrategy.Static` + 按序遍历 | ✅ `TraversalFSM.HandleBranchAsync` :412-447 | Root 改为 Static，children = injected + dyn_fallback |
| `StaticNodes` 注册到 `DictionaryNodeRegistry` | ✅ `TraversalEngine.CompilePlan` :187-206 | 注入节点加到 `StaticNodes`，自动注册 |
| `DynamicMatcher.MatchAll` first-match-wins | ✅ `DynamicMatcher` :51-72 | Skip rule 插在模板 rule 之前 |
| `TargetFound` 基于 `Node.Name` 匹配 | ✅ `TraversalEngine` :374-415 | 叶子节点 Name = target label，中间节点 Name ≠ target |
| `Operation` Click + `Target(Coordinate)` | ✅ `OperationDispatcher` | 注入节点直接带坐标 |
| `Operation` Click + `Target(Text)` → 运行时解析坐标 | ✅ `TraversalFSM.ResolveTextTarget` :232-277 | 注入节点带文本 target，引擎按 exact → contains 匹配 |
| `Precondition` 默认为 pass | ✅ `HandlePreconditionCheckAsync` :164-172 | 注入节点不需要 Precondition |
| `ErrorPolicy` Retry + fallback | ✅ `ErrorHandler` | 注入节点设 Retry, MaxRetries=1 |
| JSON round-trip 坐标 materialize | ✅ `ScenarioPlanLoader.Load` :37-57 | `plan.json` 中的坐标经 Loader 恢复为 `Coordinate` |

**FSM 不需要任何修改。** 引擎只看到一个更丰富的 TraversalPlan。

## 9. 测试策略

### 9.1 单元测试（PlanOptimizationAdvisorTests）

Mirror `IntentExtractorTests`（14 tests），使用 `StubHttpHandler` + `OpenAiCompatibleVisionProvider`：
- 成功解析含坐标的 static_nodes
- 成功解析含 text target 的 static_nodes
- 成功解析含 skip_entries
- 成功解析嵌套 children
- 空 static_nodes + 空 skip_entries
- 无效 JSON → InvalidOperationException
- 坐标越界 → 校验失败
- node_id 非法格式 → 校验失败
- TargetBy 未知值 → 校验失败
- HTTP 500 → InvalidOperationException
- 空参数校验
- Prompt 变量替换正确性

### 9.2 仿真 E2E 测试（AIOptimizationSimulationTests）

使用 `AIIntentSimulationTests` 的 `SettingsAppFixture` 和 `CreateEngine` 模式：

| # | 场景 | AI 输出 | 预期 |
|---|---|---|---|
| 1 | Locate + 注入坐标 | `static_nodes: [{name:"About phone", x:0.5, y:0.44}]` | TargetFound 早停，Battery 未访问 |
| 2 | Enumerate + 注入路径 | `static_nodes: [{name:"Storage"}]` + dyn_fallback | AllVisited，Storage 优先访问，其他 5 个一级菜单项也访问了 |
| 3 | Skip-rule 注入 | `skip_entries: ["Battery"]` | 引擎跳过 Battery，其他 4 个菜单项全访问 |
| 4 | 空 hints No-op | `static_nodes: [], skip_entries: []` | Plan 结构与无 AI 版本等价 |
| 5 | Advisor 失败 fallback | stub 500 | 回退无优化 plan，引擎正常完成 |
| 6 | JSON round-trip | 完整注入链路 | ToJson → FromJson → ScenarioPlanLoader → engine 可执行 |

### 9.3 真实 Sensenova 测试（opt-in, 需 SENSENOVA_API_KEY）

| # | 场景 | 验证 |
|---|---|---|
| 7 | Locate About phone | AI 返回的 JSON 包含合理的 static node（name 含 "About phone"）且注入后 engine 正常执行 |
| 8 | Enumerate Settings | AI 返回的 skip_entries 不包含明显的导航项（如 "Wi-Fi"） |

## 10. 未来扩展：历史数据（Phase 2）

当前 artifacts 是只写的。Phase 2 增加：

```
src/UniClaw.Host/Artifacts/RunHistoryReader.cs
  LoadAsync(outputRoot, scenarioId, maxRuns=5)
    → 遍历 artifacts/runs/<scenarioId>/*/manifest.json
    → 读 result.json
    → 聚合为 RunHistorySummary（紧凑 JSON ≤ 2000 chars）
    → 作为 {history} 变量注入 prompt
```

`IPlanOptimizationAdvisor.SuggestAsync` 的签名预留了 `RunHistorySummary?` 参数（Phase 1 为 null）。

## 11. 风险评估

| 风险 | 缓解 |
|---|---|
| AI 幻视坐标 | 优先 text target；坐标只在高度确定时用；失效 fallback 到 dyn_fallback |
| skip_entries 误包含 target | Injector 校验 skip_entries 不包含 target label |
| 中间节点 Name 触发 TargetFound 误匹配 | 中间节点 Name = entry label，叶子节点 Name = target label |
| DynamicRule 顺序依赖 | Injector 显式重建 Dictionary，不依赖插入顺序的隐式行为 |
| Sensenova 不可用 | Advisor 返回 null / 异常 catch → 纯确定性 plan |
