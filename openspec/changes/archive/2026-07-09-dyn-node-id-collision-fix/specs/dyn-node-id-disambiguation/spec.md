## ADDED Requirements

### Requirement: Dynamic child node IDs SHALL include parent node ID for cross-page disambiguation

DynamicMatch 子节点 NodeId 生成公式 SHALL 为 `dyn_{template}_{itemText}_{parentNodeId}`，其中 `parentNodeId` 是触发 DynamicMatch 规则的父节点的 NodeId。此公式 SHALL 在两处统一使用：TraversalEngine.GenerateDynamicChildren() 中 container 节点 ID 生成，以及 TemplateInstantiator.Instantiate() 中 leaf 节点 ID 生成。

当不同页面的元素具有相同显示文本时（如 Wi-Fi 开关 "ON" 和 Bluetooth 开关 "ON"），生成的 NodeId SHALL 不同，因为父节点 ID 不同（如 `dyn_switch_leaf_ON_dyn_menu_container_Wi-Fi` vs `dyn_switch_leaf_ON_dyn_menu_container_Bluetooth`）。

#### Scenario: Wi-Fi and Bluetooth switches with same text generate distinct NodeIds
- **WHEN** DynamicMatch root node matches Wi-Fi page switch (text="ON", parent=dyn_menu_container_Wi-Fi) and Bluetooth page switch (text="ON", parent=dyn_menu_container_Bluetooth)
- **THEN** Wi-Fi switch NodeId SHALL be `dyn_switch_leaf_ON_dyn_menu_container_Wi-Fi` and Bluetooth switch NodeId SHALL be `dyn_switch_leaf_ON_dyn_menu_container_Bluetooth` — distinct IDs, no collision

#### Scenario: Same-named container children under different parents
- **WHEN** menu_rule generates a child for "Wi-Fi" button under root (parent=root) and another child for "Wi-Fi" under a different parent (hypothetical nested menu)
- **THEN** first child NodeId SHALL be `dyn_menu_container_Wi-Fi_root` and second SHALL be `dyn_menu_container_Wi-Fi_{other_parent_id}` — distinct IDs

#### Scenario: Root node as parent produces short ID suffix
- **WHEN** DynamicMatch root (NodeId="root") generates a menu_container child for "Wi-Fi" button
- **THEN** child NodeId SHALL be `dyn_menu_container_Wi-Fi_root` — suffix is the parent's NodeId

### Requirement: Dynamic child dedup key SHALL use same-page childName (without parent context)

DynamicChildManager._generatedPairs 的 dedup key SHALL 使用不含父节点 ID 的 childName（格式 `{template}_{itemText}`），与 NodeId 生成公式不同。NodeId 需跨页面唯一（含 parentNodeId），dedup 需同页面防重复（不含 parentNodeId）。两者机制独立，公式不同。

#### Scenario: Dedup prevents duplicate generation within same page
- **WHEN** Generate() processes match results for the same parent node on the same page fingerprint with same template and itemText
- **THEN** first result SHALL generate a child node, second result SHALL be deduplicated via _generatedPairs check using the page-level childName (format `{template}_{itemText}`, no parent context)

#### Scenario: Dedup does NOT prevent different pages generating same-text children
- **WHEN** Generate() processes Wi-Fi page (fingerprint A) generating "ON" switch child and Bluetooth page (fingerprint B) also generating "ON" switch child
- **THEN** both SHALL be generated (different fingerprints produce different dedup keys), resulting in distinct NodeIds: `dyn_switch_leaf_ON_dyn_menu_container_Wi-Fi_root` and `dyn_switch_leaf_ON_dyn_menu_container_Bluetooth_root`

### Requirement: Full traversal SHALL visit all dynamically generated nodes without false "already visited" skips

TraversalEngine 全量遍历（CompletionPolicy=null, AllChildrenVisited exit condition）SHALL 实际访问所有 DynamicMatch 生成的子节点。引擎 SHALL NOT 因 NodeId 碰撞而将未访问的节点误判为已访问。

#### Scenario: Settings App 7-page full traversal visits all 19 nodes
- **WHEN** TraversalEngine runs full traversal on 7-page Settings App fixture with DynamicMatch root (menu_rule + switch_rule)
- **THEN** VisitedPages count SHALL be 19 (root + 6 level-1 containers + 3 switch leaves + 8 level-2 containers + 1 Bluetooth switch leaf that was previously skipped)
- **THEN** VisitedPages SHALL contain a disambiguated NodeId for Bluetooth switch (e.g., containing both "ON" and "Bluetooth" in the NodeId)
- **THEN** CompletionReason SHALL be "all_visited"

#### Scenario: Target search still terminates early at Dark mode
- **WHEN** TraversalEngine runs target search on 7-page Settings App with CompletionPolicy=TargetFound "Dark mode" Exact MarkAndStop
- **THEN** CompletionReason SHALL be "target_found" and Storage subtree SHALL NOT be visited — early termination proof unaffected by NodeId change
