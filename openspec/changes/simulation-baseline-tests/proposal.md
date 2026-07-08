## Why

C# 当前只有开发验证级的 SimulationE2ETests.cs (2-page/4-page 简化 fixture)，缺失 Python 等价的 7 页 Settings App fixture、全量遍历基线测试、目标搜索基线测试和 SimulationBaselineTests.cs。constitution C-11 要求基线 E2E 回归门槛，但没有对应的功能回归 guard 测试代码。

## What Changes

- 新建 `SimulationBaselineTests.cs` (tests/.../Baseline/ 目录)，包含 2 个核心场景测试:
  - 场景 1: Settings 全量遍历 (AllVisited, CompletionPolicy=null)
  - 场景 2: Settings 目标搜索 (TargetFound, CompletionPolicy=TargetFound "Dark mode" Exact)
- 新建 7 页 Settings App fixture (private static StateFixtureBuilder 方法，内联在测试文件)
- 断言策略: Phase B 先用范围断言 (≥, Contains, DoesNotContain)，Phase C 升级为精确值
- TargetFound 匹配使用 Operation.Target.Value (元素文本)，不是 Name (template ID)

## Capabilities

### New Capabilities
- `simulation-baseline`: 7 页 Settings App fixture + 2 核心基线场景测试 (全量遍历 + 目标搜索) + 范围断言策略

### Modified Capabilities
- `traversal-engine`: 场景 2 依赖 TargetFound 终止能力 (由 completion-policy-impl change 提供)

## Impact

- **代码**: 1 个新文件 SimulationBaselineTests.cs (Baseline 目录)
- **测试数**: 新增 2 个基线测试
- **依赖**: Phase A (completion-policy-impl) 必须先完成 — 场景 2 需要 TargetFound 终止
- **文档**: simulation-baseline.md §3 缺口状态表更新
