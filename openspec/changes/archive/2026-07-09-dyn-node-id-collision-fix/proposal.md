## Why

DynamicMatch 子节点 ID 生成公式 `dyn_{template}_{item_text}` 仅依赖模板名和元素文本，不含父节点上下文。当不同页面存在相同文本元素时（如 Wi-Fi 页和 Bluetooth 页的开关都显示 "ON"），生成的 NodeId 完全相同（`dyn_switch_leaf_ON`），导致引擎将第二次遇到的同名节点标记为 "已访问" 并跳过。全量遍历基线测试因此漏掉 Bluetooth 开关（18 visited vs 应有 19），但引擎仍报告 `all_visited`——功能回归 guard 被绕过。

## What Changes

- **BREAKING**: 动态子节点 ID 生成公式变更 — 从 `dyn_{template}_{item_text}` 改为包含父节点 ID 的消歧格式
- 修复 `TraversalEngine.GenerateDynamicChildren()` 中 container 节点 ID 生成 (L537)
- 修复 `TemplateInstantiator.Instantiate()` 中 leaf 节点 ID 生成 (L55)
- 基线测试断言需更新为新的 NodeId 格式
- 基线数值文档需更新为 C# 实际基线值 (18→19 visited pages)

## Capabilities

### New Capabilities
- `dyn-node-id-disambiguation`: 动态子节点 ID 消歧规则 — 定义 NodeId 生成公式、碰撞检测、消歧后 DFS 行为

### Modified Capabilities
- `traversal-engine`: TraversalEngine.GenerateDynamicChildren 和 TemplateInstantiator.Instantiate 的 NodeId 生成行为变更

## Impact

- **核心代码**: `TraversalEngine.cs` (GenerateDynamicChildren ~L537), `TemplateInstantiator.cs` (Instantiate ~L55)
- **测试**: `SimulationBaselineTests.cs` (VisitedPages 断言需适配新 NodeId 格式), `TraversalEngineTests.cs`, `SimulationE2ETests.cs`
- **文档**: `docs/system/layers/simulation-baseline.md` (基线数值更新), `docs/system/layers/traversal.md` (NodeId 生成规则)
- **API**: 无外部 API 变化 — NodeId 是内部概念，不暴露给上层
- **回归风险**: 所有使用 DynamicMatch 的遍历场景的 VisitedPages/ActionHistory 格式变化
