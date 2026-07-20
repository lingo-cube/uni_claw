## Context

SimulationBaselineTests 的 2 个核心场景 (C-11 CI-blocking) 失败:
- FullTraversal: 14/18 elements matched — missed [device_2, dark_mode, network_2, network_3]
- TargetSearch: CompletionReason=all_visited 而非 target_found; forbidden pages visited

两个引擎层 bug 阻断 DFS 完整遍历:

**Bug 1 — DynamicChildManager dedup scope 过宽**: `_generatedPairs` 使用 `(fingerprint, childName)` 作为 dedup key。当 wifi 页面的 menu_container 子节点 (network_1) 尝试从同一页面 (相同 fingerprint) 生成 DynamicMatch 子节点时, 所有 childName (switch_leaf_ON, menu_container_HomeNetwork 等) 已存在于 dedup 集合中 → 0 子节点 → 容器提前完成。NodeId 生成 (`dyn_{template}_{text}_{parentId}`) 本身产生唯一 ID, 但 dedup key 不含 parentId, 导致不同父节点在同一页面的合法子节点被误拒。

**Bug 2 — InterceptionHandler 无条件 PressBack**: `OnDynamicMatchNodeSelect` 在非 root DynamicMatch 子节点耗尽时 (depth > 1), 无论是否发生页面导航, 都执行 `PressBackAsync + Stack.Pop()`. 无导航时 PressBack 物理回退到父页面, 使父帧缓存指纹失配 → 剩余子节点无法访问 → 遍历提前终止。

两个 bug 联动: Bug 1 使 menu_container 退化为 leaf → Bug 2 的 PressBack 路径被触发 → 父帧指纹失配 → 4 元素丢失 → TargetFound 无匹配目标。

## Goals / Non-Goals

**Goals:**
- 修复 dedup scope 使嵌套 DynamicMatch 在同页面上独立生成子节点
- 修复 PressBack 逻辑使无导航时仅 Pop 不 PressBack
- 使 2 个 SimulationBaselineTests 场景从 FAIL→PASS (C-11)
- CompletionPolicy TargetFound 自然生效 (无 spec 变更)
- 重校准 baseline JSON numericAnchor + elementCoverage 数值

**Non-Goals:**
- 不改 CompletionPolicy spec (上游 DFS 修后自然生效)
- 不改 ExpectedBehavior Verify 逻辑 (7 类验证维度不变)
- 不改 mock 服务 (StatefulMockVisionService/StatefulMockActionExecutor)
- 不改 numericAnchor tolerance 机制 (±5% 不变)
- 不修 PlanCompiler (dormant, 独立 change)

## Decisions

### D-89: dedup key 改为 `(parentNodeId, childName)` 而非 `(fingerprint, childName)`

**选择**: `(parentNodeId, childName)`
**替代方案**:
- A: `(parentNodeId, childName)` — 精确 scope, 同一父节点同页面仅生成一次, 不同父节点独立
- B: `(parentNodeId, fingerprint, childName)` — 含 fingerprint, 不同页面同一父节点可重新生成 (但 Invalidate 已清缓存, 页面级 dedup 不需要)
- C: 仅 `(childName)` — 全局 dedup, 跨页面同名元素被误拒

**理由**: A 最简洁且满足所有场景。Invalidate 清缓存后 Generate 重新调用, dedup pair 含 parentNodeId 防止同一父节点重复生成。不同父节点 (wifi_subframe vs network_1_menu_container) 即使在同一页面也独立生成 — 这是正确行为 (NodeId 不同)。选择 B 多了 fingerprint 但 Invalidate 后指纹可能变, dedup 反而可能漏。选择 C 是当前 bug 的极端版。

**影响**: `_generatedPairs` 从 `HashSet<(string fingerprint, string name)>` 改为 `HashSet<(string parentNodeId, string name)>`. Generate 方法中 `node.NodeId` 已可获取, 无新增依赖。

### D-90: PressBack 条件改为 "指纹变化时才 PressBack"

**选择**: 比较 cached fingerprint vs current fingerprint, 仅不同时 PressBack
**替代方案**:
- A: 指纹比较 (缓存的帧指纹 vs 当前页面指纹) — 精确, 与 D-74 导航检测逻辑一致
- B: 比较栈帧 EntryPage vs CurrentPage — 需要 EntryPage 追踪, 新增状态
- C: 比较栈帧 children 的页面归属 — 过于复杂
- D: 移除 PressBack, 非 root 子节点耗尽一律 Pop-only — 过于激进, 导航子页帧确实需要回退

**理由**: A 与 D-74 (TryHandleNavigation) 的指纹比较逻辑完全一致, 复用 PageSnapshotManager.Fingerprint 方法, 零新增依赖。B 需要追踪每个栈帧的 entry page ID, 增加可变状态。D 会破坏导航子页帧的回退路径。

**实现**:
1. 在 `OnDynamicMatchNodeSelect` 的 depth>1 分支, 获取 `cachedFingerprint` (从 ChildMgr.GetCachedFingerprint)
2. 获取 `currentFingerprint` (从 SnapshotMgr.Fingerprint(runtimeCtx?.CurrentPageAnalysis))
3. 如果 `cachedFingerprint != 0 && cachedFingerprint != currentFingerprint` → PressBack+Pop
4. 否则 → Pop-only (无 PressBack)

**注意**: 当 ChildMgr 缓存不存在 (Generate 未调用) 时 GetCachedFingerprint 返回 null, 表示首次进入 — 此时也不应 PressBack (首次不可能有导航, 因为刚进这个帧)。

## Risks / Trade-offs

- **[Risk] dedup scope 改变可能允许同一父节点重复生成子节点** → Mitigation: parentNodeId scope + Invalidate 后 Generate 重新调用时, 同一父节点同一 childName 仍被 dedup 拒 (正确行为: 防止同一子节点重复创建)。Invalidate 不清 `_generatedPairs` (D-3), 所以 re-generation 时已生成 pair 仍生效。

- **[Risk] PressBack 条件改变可能导致导航子页帧不回退** → Mitigation: 仅当 cached fingerprint == current fingerprint 时 Pop-only; fingerprint 变化 (导航后) 仍 PressBack。D-74 TryHandleNavigation 在更早阶段检测导航并推子页帧, 此路径不受影响。

- **[Risk] baseline 数值大幅变化 (步数/节点数增加)** → Mitigation: 修复后 DFS 应遍历更多节点, 步数增加是正确行为。JSON 数值用 ±5% tolerance, CI 环境波动不会误杀。`elapsedSecondsMax` 取 generous 值。

- **[Trade-off] menu_container 子节点仍可能有 0 个 DynamicMatch 子节点** → 接受: wifi 页面的 network_1 是一个按钮, 点击后无导航。DynamicMatch 为它生成子节点 (dedup 不再阻止), 但子节点与 wifi_subframe 的子节点重叠 (switch_leaf_ON, menu_container_HomeNetwork 等)。不过这些子节点可能已被 visited → GetNextUnvisitedChild 返回 null → 容器提前完成 → Pop-only。这是合理的 — 非导航按钮不需要子页帧。
