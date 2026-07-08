## Context

C# 当前有 SimulationE2ETests.cs (7 个开发验证级场景，2-page 和 4-page 简化 fixture)。Python 有完整的 7+2 页 Settings App fixture + 2 核心基线场景 (118步/19节点全量遍历, 49步/9节点目标搜索)。constitution C-11 定义了基线 E2E 回归门槛原则，但缺少对应的功能回归 guard 测试代码。

Phase A (completion-policy-impl) 实现了 RunAsync CompletionPolicy 检查逻辑，包括 TargetFound 匹配 (Operation.Target.Value) 和 Timeout/MaxSteps 终止。Phase B 在此基础上构建基线测试。

关键约束: Python 基线数值 (118步/19节点) 是参考锚点，不是 C# 精确基线。C# DFS 顺序、步数可能与 Python 不同。Phase B 用范围断言，Phase C 运行后升级为精确值。

## Goals / Non-Goals

**Goals:**
- 新建 SimulationBaselineTests.cs 含 2 核心基线场景
- 7 页 Settings App fixture (StateFixtureBuilder Fluent API)
- 场景 1: 全量遍历 (AllVisited, CompletionPolicy=null, DynamicMatch root)
- 场景 2: 目标搜索 (TargetFound "Dark mode" Exact MarkAndStop, 同 fixture + 同 root)
- 范围断言策略 (Phase B: ≥, Contains, DoesNotContain; Phase C: 精确值)

**Non-Goals:**
- 精确基线数值断言 (Phase C，待 C# 实际运行后确认)
- 7 类规则验证框架 (Python ExpectedBehavior 等价)
- DFS 顺序断言 (Phase C)
- ExecuteThenStop 完整实现 (Phase 3)
- settings-app.json 外部 fixture 文件 (fixture 内联在测试方法)

## Decisions

### D-B1: Fixture 内联为 private static 方法，不单独建 fixture 文件

**选择**: `SimulationBaselineTests.cs` 中的 `private static StateFixture SettingsAppFixture7Pages()` 方法
**理由**: 基线测试专用 fixture，其他测试不会复用；内联减少文件数和维护负担
**替代方案**:
- 独立 fixture 文件 → ❌ 当前只有基线测试用，不值得独立文件
- JSON fixture 数据 → ❌ C# 用 Fluent Builder 更自然，不需要外部 JSON

### D-B2: 两个场景共享同一 root 和 fixture

**选择**: 场景 1 和 2 用同一个 SettingsAppFixture7Pages() 和同一个 DynamicMatch root node
**理由**: 区别仅在 TraversalPlan.CompletionPolicy — null vs TargetFound。共享 fixture 保证 DFS 路径一致性
**替代方案**:
- 分开构造 root → ❌ 不必要的代码重复，且 DFS 路径可能不同导致对比失效

### D-B3: 范围断言而非精确值断言

**选择**: `result.VisitedPages.Count >= 7` + `Assert.Contains` + `Assert.DoesNotContain`
**理由**: C# 实际数值可能与 Python 不同 (DFS 顺序、元素映射差异)。先范围后精确，避免测试不稳定
**替代方案**:
- 精确值断言 (`Count == 19`) → ❌ C# 基线未确认，过早精确化会导致测试频繁失败
- 不做任何数值断言 → ❌ 无法验证功能正确性

### D-B4: 场景 2 未访问项作为提前终止证明

**选择**: `Assert.Contains("Display", result.VisitedPages)` + `Assert.DoesNotContain("Storage", result.VisitedPages)`
**理由**: Display 被访问证明 DFS 到了目标子树；Storage 未被访问证明命中后不再继续。两点共同证明 MARK_AND_STOP 生效
**替代方案**:
- 仅断言 CompletionReason == TargetFound → ❌ 不证明 DFS 提前终止，可能是巧合命中

## Risks / Trade-offs

- [C# 与 Python 基线数值差异] → Phase B 用范围断言容忍差异，Phase C 升级为精确值；数值确认后同步更新 simulation-baseline.md
- [TargetFound 匹配依赖 Operation.Target.Value] → Phase A 提供 TargetFound 检查，但匹配字段用 Operation.Target.Value (不是 Name)；需确保 DynamicChildManager 正确传递 item_text 到模板解析
- [7 页 fixture 可能有元素映射差异] → Python brightness 是 slider, C# mock 暂映射为 switch；不影响遍历逻辑
