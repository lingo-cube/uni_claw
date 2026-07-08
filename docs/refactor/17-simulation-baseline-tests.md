# 17 — 仿真基线测试：7 页 Fixture + 2 核心场景

> 类型: refactor design (HOW)
> 依赖: → `docs/system/layers/simulation-baseline.md` (WHAT — 场景定义 + fixture 规格 + 基线数值)
> 依赖: → `docs/refactor/16-completion-policy-impl.md` (Phase A — CompletionPolicy 实现)
> 约束: → constitution C-11 (基线 E2E 回归门槛)
> Python 对齐: Python V6.11.0 `test_settings_simulation.py` + V6.11.1 `test_target_search.py`

---

## 0. 问题陈述

C# 当前只有 4 页简化版 fixture 的 `SimulationE2ETests.cs`（开发验证级，非基线级）。缺失：

| 缺口 | 说明 |
|------|------|
| 7 页 Settings App fixture | Python 有 7+2 页完整结构，C# 只有 2-page 和 4-page |
| 全量遍历基线测试 | 7 类规则验证 + 数值断言 |
| 目标搜索基线测试 | DFS 顺序 + TARGET_FOUND 提前终止 + 未访问项证明 |
| SimulationBaselineTests.cs | 完全不存在 |
| C-11 基线约束 guard | constitution 中未写入基线 CI-blocking 约束 |

Phase A (`docs/refactor/16-completion-policy-impl.md`) 补了 CompletionPolicy 检查逻辑和 TargetFound/Timeout 常量。Phase B 在此基础上构建基线测试。

---

## 1. 7 页 Settings App Fixture

### 1.1 Fixture 定义

直接使用 simulation-baseline.md §1.0 中已有的 StateFixtureBuilder 代码：

```
Pages: home, wifi, bluetooth, display, storage, storage_internal, storage_external
Elements: 22 (6+3+3+3+2+3+2)
Transitions: 11 (6 forward + 4 back + 2 sub-page)
```

### 1.2 代码位置

Fixture 作为 `SimulationBaselineTests.cs` 中的 private static 方法，不单独建 fixture 文件：

```csharp
private static StateFixture SettingsAppFixture7Pages() => new StateFixtureBuilder()
    .Page("home", p => p
        .Name("Settings")
        .Button("menu_wifi", "Wi-Fi", 0.50, 0.13)
        // ... 照 simulation-baseline.md §1.0 完整代码
    )
    // ... 全 7 页 + 11 transition
    .Build();
```

---

## 2. 场景 1: Settings 全量遍历基线测试

### 2.1 测试签名

```csharp
[Fact]
public void SettingsApp_FullTraversal_AllVisited()
```

### 2.2 TraversalPlan 配置

DynamicMatch root + menu_rule/switch_rule（同 simulation-baseline.md §1.1）：

```csharp
var root = new TraversalNode("root", "Settings App", NodeType.Container,
    new Operation(OperationType.NoAction),
    new ChildrenStrategy(ChildrenStrategyType.DynamicMatch,
        DynamicRules: new Dictionary<string, DynamicRule>
        {
            ["menu_rule"] = new DynamicRule(
                RuleId: "menu_rule",
                MatchCondition: new MatchCondition(Type: "menu_item"),
                ChildTemplate: "menu_container",
                Action: "generate_child"),
            ["switch_rule"] = new DynamicRule(
                RuleId: "switch_rule",
                MatchCondition: new MatchCondition(Type: "switch"),
                ChildTemplate: "switch_leaf",
                Action: "generate_child"),
        }),
    ExitCondition: new ExitCondition(
        Type: ExitConditionType.AllChildrenVisited,
        Fallback: FallbackAction.AutoEscape));

var plan = new TraversalPlan(
    EntryApp: "com.example.settings",
    EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
    PlanName: "Safe Full Traversal",
    PlanId: "settings-full-traversal-v1",
    RootNode: root,
    StaticNodes: new Dictionary<string, TraversalNode>());
```

CompletionPolicy: **null**（自然完成）。

### 2.3 断言

Phase B 先用**范围断言**，避免 C# 与 Python 数值差异导致测试不稳定：

| 断言维度 | 断言方式 | 说明 |
|---------|---------|------|
| completion | `result.Success == true` | 成功完成 |
| completion | `result.CompletionReason == Reasons.AllVisited` | 自然完成原因 |
| visited_pages | `result.VisitedPages.Count >= 7` | 至少访问 7 个页面 |
| visited_pages | `Assert.Contains("home", result.VisitedPages)` | 首页必访问 |
| visited_pages | `Assert.Contains("wifi", result.VisitedPages)` | WiFi 页必访问 |
| total_steps | `result.TotalSteps > 0` | 有实际执行 |
| action_history | `result.ActionHistory.Length > 0` | 有操作记录 |

> Python 基线值 (118 步/19 节点) 是**参考锚点**，不是 C# 精确基线。C# 实际值待运行确认后更新为精确基线（Phase C）。

---

## 3. 场景 2: Settings 目标搜索基线测试

### 3.1 测试签名

```csharp
[Fact]
public void SettingsApp_TargetSearch_StopsAtDarkMode()
```

### 3.2 TraversalPlan 配置

与场景 1 共享同一 root 和 fixture，仅 CompletionPolicy 不同：

```csharp
var plan = new TraversalPlan(
    EntryApp: "com.example.settings",
    EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
    PlanName: "Target Search - Dark Mode",
    PlanId: "settings-target-search-v1",
    RootNode: root,  // 同场景 1
    StaticNodes: new Dictionary<string, TraversalNode>(),
    CompletionPolicy: new CompletionPolicy(
        Type: CompletionPolicyType.TargetFound,
        TargetName: "Dark mode",
        MatchMode: MatchMode.Exact,
        ActionOnFound: TargetFoundAction.MarkAndStop));
```

### 3.3 断言

| 断言维度 | 断言方式 | 说明 |
|---------|---------|------|
| completion | `result.Success == true` | 目标搜索成功 |
| completion | `result.CompletionReason == Reasons.TargetFound` | 目标命中终止 |
| visited_pages | `Assert.Contains("Display", result.VisitedPages)` | 目标在 Display 子树下 |
| visited_pages | `Assert.DoesNotContain("Storage", result.VisitedPages)` | 提前终止证明 |
| total_steps | `result.TotalSteps > 0 && result.TotalSteps < 全量步数` | 比全量少 |

**关键证明逻辑**：
- Display 页被访问 → DFS 到了 Display 子树
- Storage 页未被访问 → DFS 在命中目标后不再继续
- 两点共同证明 MARK_AND_STOP 生效

### 3.4 目标匹配逻辑

Phase A 在 RunAsync 中检查 `_ctx.CurrentFrame` 的 **`Operation.Target.Value`** 是否匹配 `policy.TargetName`。

**⚠️ 不用 `Name` 字段**：动态节点的 `Name` = template ID (如 `"switch_leaf"`)，不是元素文本。而 `Operation.Target.Value` 经 PlaceholderResolver 解析 `{{item_text}}` 后，值为 `"Dark mode"` — 这才是用户意图匹配的正确字段。

在 simulation mock 中，DynamicMatch 的 switch_rule 为 `"dark_mode"` 开关元素生成 child node：
- `NodeId` = `"dyn_switch_leaf_Dark mode"` (复合 ID)
- `Name` = `"switch_leaf"` (template ID — **不用于匹配**)
- `Operation.Target.Value` = `"Dark mode"` (元素文本 — **匹配目标**)

当引擎访问到此节点时，`Operation.Target.Value == "Dark mode"` 与 `policy.TargetName == "Dark mode"` Exact 匹配命中，TargetFound 检查触发。

静态/root 节点可能无 `Operation.Target.Value` (如 root node Operation = NoAction)，此时 fallback 到 `Name`。

---

## 4. 测试文件结构

```
tests/UniClaw.Core.Tests/
  Baseline/
    SimulationBaselineTests.cs    ← 2 核心场景 + 7页 fixture
  Simulation/
    SimulationE2ETests.cs          ← 开发验证 E2E (不变)
  Architecture/
    ArchitectureGuardTests.cs      ← 架构约束 guard (不变)
```

Baseline 目录下的测试对应 C-11 CI-blocking 约束（后续需在 constitution/constraints.md 加 C-11 具体断言 guard）。

### 三类测试区分

| 目录 | 性质 | CI-blocking | 失败语义 | 对应文档 |
|------|------|-------------|---------|---------|
| Architecture/ | 架构约束 guard | ✅ 阻断 | 规则违反，修代码 | constitution/* |
| Baseline/ | 功能回归 guard | ✅ 阻断 | 主功能退化，修代码 | C-11 + simulation-baseline.md |
| Simulation/ | 普通 E2E / 单元 | ✅ 阻断 | 功能不工作，排查 | layers/simulation.md |

---

## 5. 断言映射原则

| 维度 | Phase B 断言方式 | Phase C 断言方式 | 说明 |
|------|----------------|----------------|------|
| completion | 精确值 (`== Reasons.AllVisited`) | 精确值 | 不变 |
| visited_pages | 范围 (`Count >= 7`) + 包含/不包含 | 精确值 (`Count == 19`) | C# 运行后升级 |
| total_steps | 范围 (`> 0`) | 精确值 (`== 118` 或 C# 实际值) | C# 运行后升级 |
| action_history | 存在性 (`Length > 0`) | 精确值 | C# 运行后升级 |
| target_found | 逻辑证明 (包含+不包含) | 精确值 + 顺序 | C# 运行后升级 |
| DFS 顺序 | 不验证 | 精确顺序断言 | Phase C |

Phase B 策略：**先范围后精确**。避免 C# 与 Python 数值差异导致测试不稳定。等 C# 实际运行确认数值后，Phase C 更新为精确基线值，同步更新 simulation-baseline.md §1 基线数值。

---

## 6. 代码改动清单

| # | 文件 | 改动类型 | 具体改动 |
|---|------|---------|---------|
| B1 | `tests/.../Baseline/SimulationBaselineTests.cs` | 新建 | 2 核心场景测试 + 7页 fixture Builder + CreateEngine helper |
| B2 | `docs/system/layers/simulation-baseline.md` §3 | 更新 | "缺口" 状态表：SimulationBaselineTests.cs 从 "不存在" 更新为 "已有" |

改动量：1 个新文件 + 1 处文档更新。

---

## 7. 依赖与前置

| 依赖 | 状态 | 所属 |
|------|------|------|
| CompletionPolicy TargetFound 检查逻辑 | Phase A 实现 | → 16-completion-policy-impl.md |
| TraversalResult.Reasons.TargetFound | Phase A 实现 | → 16-completion-policy-impl.md |
| StateFixtureBuilder 7 页构建能力 | ✅ 已实现 | Simulation namespace |
| TraversalPlan + DynamicMatch root 构造 | ✅ 已实现 | Graph namespace |
| simulation-baseline.md §1.0 fixture 定义 | ✅ 已有 | Tier 3 文档 |

Phase B 必须在 Phase A 完成后实施（场景 2 依赖 TargetFound 终止）。

---

## 8. 验证方案

1. 2 个基线测试全绿
2. 原有 516 + Phase A 5 = 521 测试不受影响
3. `dotnet test` 总测试数 = 521 + 2 = 523+
4. Phase C (后续): 运行基线测试获取 C# 实际数值，更新 simulation-baseline.md §1 精确基线值 + 断言升级为精确值
