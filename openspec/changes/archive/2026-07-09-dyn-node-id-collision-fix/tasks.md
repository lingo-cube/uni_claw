## 1. 核心代码修复 — NodeId 公式变更

- [x] 1.1 修改 `TraversalEngine.GenerateDynamicChildren()` L537: container 节点 NodeId 从 `dyn_{template}_{itemText}` 改为 `dyn_{template}_{itemText}_{node.NodeId}`（node 是当前正在生成子节点的父节点）
- [x] 1.2 修改 `TraversalEngine.GenerateDynamicChildren()` L523: childName（dedup pair 用）— **REVERTED**: dedup childName 保留旧格式 `{template}_{itemText}`，不含父节点。原因: 含父节点会破坏同页面消歧，导致 DFS 无限循环（HomeNetwork 递归嵌套）。NodeId 消歧 ≠ dedup 消歧，两者机制不同。
- [x] 1.3 修改 `TemplateInstantiator.Instantiate()` L55: leaf 节点 NodeId 从 `dyn_{template.TemplateId}_{item_text}` 改为 `dyn_{template.TemplateId}_{item_text}_{parentNodeId}`，需要将 parentNodeId 作为 context 参数传入（新增 `parent_node_id` context key）
- [x] 1.4 修改 `TraversalEngine.GenerateDynamicChildren()` 调用 `_instantiator.Instantiate()` 处: 在 instantiatorContext 中新增 `"parent_node_id"` = `node.NodeId`

## 2. 基线测试更新

- [x] 2.1 运行诊断测试（临时 BaselineDiagnosticTests），记录消歧后 C# 实际基线值（VisitedPages 数量、顺序、TotalSteps、ActionHistory）
- [x] 2.2 更新 `SimulationBaselineTests.cs` 场景1 全量遍历断言: VisitedPages.Length >= 7 → == {actual_count}, Contains 断言适配新 NodeId 格式
- [x] 2.3 更新 `SimulationBaselineTests.cs` 场景2 目标搜索断言: 验证消歧后 Bluetooth 开关未被误访、Storage 仍未访问（证明提前终止不受影响）
- [x] 2.4 更新 `docs/system/layers/simulation-baseline.md` §1.1 和 §1.2 基线数值为 C# 实际值

## 3. 其他测试适配

- [x] 3.1 更新 `SimulationE2ETests.cs` 中依赖 dyn_ NodeId 格式的断言 — 无需更新: SimulationE2ETests 不引用 dyn_ NodeId 格式
- [x] 3.2 更新 `TraversalEngineTests.cs` 中依赖 dyn_ NodeId 格式的断言 — 无需更新: TraversalEngineTests 不引用 dyn_ NodeId 格式

## 4. 验证

- [x] 4.1 `dotnet test` 全量运行，所有测试全绿（含基线 + 架构 guard + E2E + 单元）
- [x] 4.2 基线场景1 VisitedPages 包含消歧后的 Bluetooth 开关 NodeId，总数 >= 19 — Verified: VisitedPages.Length == 19, Contains "Bluetooth" && "ON"
- [x] 4.3 基线场景2 目标搜索 CompletionReason = target_found，Storage 未被访问 — Verified: target_found + DoesNotContain "Storage"
