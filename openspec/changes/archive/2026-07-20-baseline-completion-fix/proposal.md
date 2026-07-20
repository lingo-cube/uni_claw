## Why

两个核心 SimulationBaselineTests 失败 (C-11 CI-blocking guard):
- **SettingsApp_FullTraversal_AllVisited**: element_coverage 14/18 — missed [device_2, dark_mode, network_2, network_3]
- **SettingsApp_TargetSearch_StopsAtDarkMode**: CompletionPolicy TargetFound 未触发 — reason=all_visited 而非 target_found; forbidden pages (Storage/Internal Storage/SD Card) 被访问

根本原因: DFS 遍历在子页面跳过部分元素, 导致 TargetFound 永远匹配不到 "Dark mode"。引擎层存在两个 bug:

1. **DynamicChildManager dedup scope 过宽**: dedup key `(fingerprint, childName)` 阻止同页面不同父节点的合法子节点生成 — menu_container 子节点 (如 network_1) 的 DynamicMatch 在同一 wifi 页面上无法生成任何子节点 (全部被 dedup 拦截), 使其退化为 leaf 节点。
2. **InterceptionHandler 非 root 无导航时 PressBack**: 当非 root DynamicMatch 子节点耗尽且无导航发生 (页面未变), 引擎仍执行 PressBack → 物理回退到父页面 → 父帧 DynamicMatch 缓存指纹失配 → 遍历提前终止。

现在修: 这两个 bug 是 baseline 回归根因, C-11 要求 baseline 全绿才能合并。

## What Changes

- **修复 DynamicChildManager dedup scope**: dedup key 从 `(fingerprint, childName)` 改为 `(parentNodeId, childName)`, 允许不同父节点在同一页面上生成同名但不同 NodeId 的子节点
- **修复 InterceptionHandler PressBack 逻辑**: 非 root DynamicMatch 子节点耗尽时, 仅在导航确实发生 (指纹变化) 时 PressBack; 无导航时仅 Pop stack 不 PressBack
- **重校准 baseline JSON**: 修复后 engine 步数/节点数/ActionHistory 数值变化, 更新 settings-full-traversal.json + settings-target-search.json 的 numericAnchor + elementCoverage
- **更新 simulation-baseline.md**: 反映新基线数值和修复说明

## Capabilities

### New Capabilities
- `dfs-child-dedup-fix`: 修复 DynamicChildManager dedup scope 使嵌套 DynamicMatch 正确生成子节点
- `dfs-back-logic-fix`: 修复 InterceptionHandler 非 root 无导航时不应 PressBack 的逻辑

### Modified Capabilities
- `simulation-baseline`: 更新基线数值和 JSON 预期文件 (numericAnchor, elementCoverage)
- `completion-policy-check`: TargetFound 检查现在能正确触发 (修复上游 DFS bug 后自然生效, spec 不变)

## Impact

- **代码**: `DynamicChildManager` (dedup key), `InterceptionHandler` (OnDynamicMatchNodeSelect PressBack 逻辑)
- **测试**: SimulationBaselineTests 2 个场景从 FAIL→PASS; baseline JSON 预期文件数值更新; simulation-baseline.md 基线数值更新
- **API**: 无破坏性变更 — DynamicChildManager._generatedPairs 内部数据结构变更, 不影响公开接口
- **依赖**: 无新依赖
