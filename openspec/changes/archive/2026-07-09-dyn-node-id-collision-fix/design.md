## Context

DynamicMatch 子节点 ID 当前生成公式为 `dyn_{template}_{itemText}`（两处：TraversalEngine.GenerateDynamicChildren L537 + TemplateInstantiator.Instantiate L55）。该公式仅依赖模板名和元素显示文本，不含任何父节点/页面上下文信息。

当不同页面存在相同文本元素时（如 Settings App 中 Wi-Fi 页和 Bluetooth 页的开关都显示 "ON"），生成的 NodeId 完全相同：`dyn_switch_leaf_ON`。引擎通过 `VisitedNodes` 集合跟踪已访问节点，第二次遇到的同名节点被误判为 "已访问" 而跳过。

当前基线测试证实了此 bug：全量遍历基线只访问 18 个节点（缺 Bluetooth 开关），但引擎报告 `all_visited`。

Python 基线没有此问题 — Python 的动态节点命名包含父节点上下文（如 `menu_container-Wi-Fi-0-root`），天然消歧。

## Goals / Non-Goals

**Goals:**
- 消除跨页面同文本元素的 NodeId 碰撞，确保全量遍历真实完成
- 保持 NodeId 可读性（人工可从 ID 判断所属页面/层级）
- 保持 backward compatibility — 旧 TraversalPlan 的静态节点不受影响
- 基线测试断言更新为消歧后的 NodeId 格式

**Non-Goals:**
- 不修改 DFS 遍历算法本身（只修 NodeId 生成）
- 不修改 TraversalResult/TraversalNode 的公开 API（NodeId 是内部概念）
- 不引入全局 NodeId 注册表或碰撞检测机制（直接在公式中消歧）
- 不处理同页面内同文本碰撞（如同一页面两个 "Settings" 按钮 — 属于元素设计问题，非引擎责任）

## Decisions

### D-1: NodeId 公式包含父节点 ID

**选择**: `dyn_{template}_{itemText}_{parentNodeId}`

**理由**:
- 父节点 ID 已包含层级信息（如 `dyn_menu_container_Wi-Fi`）
- 递归组合后自然产生唯一路径式 ID（如 `dyn_switch_leaf_ON_dyn_menu_container_Wi-Fi`）
- 无需额外数据结构，只需修改两处字符串拼接
- 与 Python 基线命名思路一致（Python: `menu_container-Wi-Fi-0-root`）

**替代方案考虑**:
- `dyn_{template}_{itemText}_{pageIndex}`: 需要全局页面计数器，多引擎实例间不一致
- `dyn_{template}_{itemText}_{GUID}`: 不可读，调试困难
- `dyn_{template}_{itemText}_{parentFingerprint}`: 过长，指纹不稳定
- 全局碰撞检测表: 增加复杂度，需要跨步状态管理

### D-2: Container 和 Leaf 使用同一公式

**选择**: 两处（TraversalEngine L537 + TemplateInstantiator L55）统一改为 `dyn_{template}_{itemText}_{parentNodeId}`

**理由**:
- Container 节点的 NodeId 现在是 `dyn_menu_container_Wi-Fi`，加入父节点 ID 后变为 `dyn_menu_container_Wi-Fi_root` — 消歧且保持可读性
- Leaf 节点的 NodeId 现在是 `dyn_switch_leaf_ON`，加入父节点 ID 后变为 `dyn_switch_leaf_ON_dyn_menu_container_Wi-Fi` — 与 Bluetooth 的 `dyn_switch_leaf_ON_dyn_menu_container_Bluetooth` 区分
- 不引入两套公式，降低认知负担

### D-3: dedup childName 不含父节点 ID

**选择**: DynamicChildManager._generatedPairs 的 dedup key 保留旧格式 `(fingerprint, childName)`，其中 childName = `{template}_{itemText}`，不含父节点 ID。

**理由**: 实现过程中发现，dedup key 含父节点 ID 会破坏同页面消歧机制。例如 HomeNetwork 按钮在 Wi-Fi 页面上，dedup key `(wifi_fingerprint, menu_container_HomeNetwork)` 应阻止重复生成。如果 dedup key 变为 `(wifi_fingerprint, menu_container_HomeNetwork_dyn_menu_container_HomeNetwork_dyn_menu_container_Wi-Fi_root)`，每层 DFS 生成唯一 key，导致无限循环（HomeNetwork 递归嵌套，max_steps=1000 碰壁）。

**NodeId 消歧 ≠ dedup 消歧**: NodeId 需跨页面唯一（Wi-Fi ON vs Bluetooth ON），dedup 需同页面防重复（HomeNetwork 在 Wi-Fi 页面只生成一次）。两者机制不同，公式也不同。

**替代方案考虑**:
- dedup 也含父节点: 导致 DFS 无限循环（已实测验证）
- dedup 含短父标识: 仍会在 HomeNetwork 级产生新 key

### D-4: 基线测试断言更新策略

**选择**: 直接更新为消歧后格式的精确断言 (Phase C)

**理由**:
- 碰撞修复后 VisitedPages 数量从 18 变为 19（Bluetooth 开关不再被跳过）
- 断言应验证消歧是否生效（如 Contains `dyn_switch_leaf_ON_dyn_menu_container_Bluetooth`）
- 不再维持 Phase B 范围断言 — 有具体 bug 修复驱动，可直接升级为精确值

## Risks / Trade-offs

- **[NodeId 变长]** → 深层嵌套场景 ID 可能很长（如 3 层 DFS 的 switch leaf）。缓解: 实际 Settings App 只有 2 层深度，ID 长度仍在合理范围内；极端深度场景可后续优化（截断 hash）
- **[测试全面更新]** → 所有依赖动态节点 ID 格式的测试需更新。缓解: 影响范围有限（SimulationBaselineTests + SimulationE2ETests + TraversalEngineTests），且断言升级到精确值是正向改进
- **[Performance 微影响]** → NodeId 字符串更长，VisitedNodes HashSet 查找微慢。缓解: 节点数通常 <100，hash 查找 O(1)，影响可忽略
