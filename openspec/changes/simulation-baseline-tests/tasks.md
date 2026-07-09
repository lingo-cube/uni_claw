## 1. 前置依赖验证

- [x] 1.1 确认 completion-policy-impl change 已完成 (TargetFound/Timeout/MaxSteps 检查逻辑 + TraversalResult.Reasons 新增)
- [x] 1.2 确认 StateFixtureBuilder 支持 7 页构建 (Button, Switch, Readonly, BackButton, Transition)

## 2. 创建测试文件与 7 页 Fixture

- [x] 2.1 创建 `tests/.../Baseline/SimulationBaselineTests.cs` 文件
- [x] 2.2 实现 `private static StateFixture SettingsAppFixture7Pages()` — 照 simulation-baseline.md §1.0 完整代码 (7 页 + 11 transition)
- [x] 2.3 实现 `private static TraversalEngine CreateEngine(StateFixture fixture, TraversalPlan plan)` helper 方法

## 3. 场景 1: 全量遍历基线测试

- [x] 3.1 构造 DynamicMatch root (menu_rule + switch_rule, ExitCondition AllChildrenVisited AutoEscape)
- [x] 3.2 构造 TraversalPlan (CompletionPolicy=null, EntryPolicy=BindCurrentScreen)
- [x] 3.3 实现 `SettingsApp_FullTraversal_AllVisited` 测试方法 + 范围断言 (Success, AllVisited, VisitedPages.Count>=7, Contains, TotalSteps>0, ActionHistory)

## 4. 场景 2: 目标搜索基线测试

- [x] 4.1 构造 TraversalPlan (CompletionPolicy=TargetFound "Dark mode" Exact MarkAndStop, 同 root)
- [x] 4.2 实现 `SettingsApp_TargetSearch_StopsAtDarkMode` 测试方法 + 断言 (Success, TargetFound, Contains Display, DoesNotContain Storage, TotalSteps < fullTraversal)

## 5. 文档更新

- [ ] 5.1 更新 `docs/system/layers/simulation-baseline.md` §3 缺口状态表: SimulationBaselineTests.cs 从 "不存在" 更新为 "已有"

## 6. 验证

- [x] 6.1 `dotnet test` 运行全部测试，新增 2 个基线测试全绿
- [x] 6.2 原有测试不受影响 (SimulationE2ETests.cs 不变)
