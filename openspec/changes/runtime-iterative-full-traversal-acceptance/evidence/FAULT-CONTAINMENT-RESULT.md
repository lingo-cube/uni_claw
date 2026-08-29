# PROJECT_LEADER_SEMANTIC_PROJECTOR_OCCURRENCE_FAULT_CONTAINMENT_RESULT

> Gate: SEMANTIC_PROJECTOR_OCCURRENCE_FAULT_CONTAINMENT · 2026-08-29

## 1. D Deterministic RED ✅

测试 `Malformed_occurrence_does_not_destroy_valid_occurrence_evidence`：
- 构造零宽度 bounds（X1=X2=0.50, Y1=0.50, Y2=0.55 — 通过 IsValid 但 SemanticNormalizedBounds 拒绝）
- OLD：Projector 抛出 → 整帧证据清空
- RED 确认 ✅

## 2. MalformedOccurrence FDP

`SemanticObservationFactProjector.AddVisionFacts()` line 62
→ `Normalize(bounds)` → `SemanticNormalizedBounds` 构造器 `width <= 0` → throw

## 3. WholeFrameEvidenceLoss FDP

`SemanticCapabilityEnvironment.ObserveAsync()` catch 子句
→ 单 occurrence 异常 → `return raw with { Evidence = Empty }` → 整帧证据丢失

## 4. Owner Derivation

**Projector** 拥有 "occurrence 是否可投影" 的语义。修复在 Projector boundary：
`bounds.X2 > bounds.X1 && bounds.Y2 > bounds.Y1` 检查 → 跳过该 occurrence 的 Geometry fact（保留 Text/ClassName facts）

## 5. Minimal Behavior Contract

`PER_OCCURRENCE_SEMANTIC_FAULT_CONTAINMENT`：
- 坏 occurrence 自己 fail-closed（无 Geometry fact → 不参与空间 pattern）
- 同帧合法 occurrence 的 evidence 不受牵连
- 坏 occurrence 保留 Text/ClassName facts → 仍可被非空间 pattern 分类

## 6. Production Symbols Changed

| Symbol | Change |
|---|---|
| `SemanticObservationFactProjector.AddVisionFacts()` | 添加 `bounds.X2 > bounds.X1 && bounds.Y2 > bounds.Y1` 检查 |

仅 1 个符号、1 行条件。零新 API、零新类型、零行为语义变更（除了故障隔离本身）。

## 7. D RED→GREEN ✅

- Malformed occurrence 测试：**RED → GREEN** ✅
- 全 capability 套件：17/17 ✅
- OpenWorld：100/100 ✅
- 全量回归：2280/2290（10 失败全部预存环境类）

## 8. Local-vs-Global Failure Counterexamples

| 场景 | 行为 | 测试 |
|---|---|---|
| 零宽度单 occurrence → 局部跳过 | ✓ Geometry fact 跳过，Text facts 保留 | ✅ |
| 全局 source 冲突 → 整帧 fail-closed | ✓ Projector 的 source 验证 throw 保持 | ✓（现有 source 测试）|
| 观测序列不匹配 → 整帧 fail-closed | ✓ staleness check 保持 | ✓（现有测试）|

## 9. Diagnostic Trace Preservation ✅

D1 的 `[semantic-diagnostic]` trace 保留且工作正常。本轮 campaign 无诊断输出（无异常发生）= 故障隔离生效。

## 10. Settings Title Deterministic Result ✅

`Settings_title_in_multi_element_observation_gets_evidence`：**PASS**
- 多元素观测中 Pattern 1 正确分类 'Settings' → NonInteractive

## 11. E Subtitle Regression ✅

全 capability 套件（含 subtitle RED→GREEN + 反例）17/17 绿。

## 12. Fresh Real Campaign ✅

- 无 semantic diagnostics（故障隔离生效，无异常）
- 终端：`Unknown interaction affordances remain`（但阻塞源已变化）

## 13. Settings Title Real Result

**未出现在 accepted 帧中** — 'Settings' (row_001) 只在 seq 2（启动帧），可能不在 viewport exploration 的 accepted 列表中。需确认 seq 2 是否进入 completeness check。

## 14. Subtitle Real Result

**✅ 副标题不再阻塞** — 本轮 campaign 中无 'Dark theme...' 类 Unknown text_block。Pattern 7 + 其他修复生效。

## 15. New Diagnostic Evidence

修复 D/E 后暴露了**下一层 Unknown 源**（此前被 D/E 掩盖）：

| 元素 | 类型 | 帧 | 根因 |
|---|---|---|---|
| icon (no text) | icon | seq 4/5 | 无 capability pattern 覆盖无文字图标 → Unknown |
| checkbox 'INCLVVUIIIILCIIICL' | checkbox | seq 4/5 | 乱码文本 checkbox → 可能未匹配 toggle pattern → Unknown |

## 16. Residual Unknown Matrix

| Occurrence | Stable? | ExpectedRole | ActualRole | Reason | FDP | Owner | ExistingContract? | NewCapability? |
|---|---|---|---|---|---|---|---|---|
| icon (textless) | ✓ | NonInteractive (decorative) | Unknown | 无 pattern | capability | SettingsSemanticCapability | 可用 NonInteractive | pattern needed |
| checkbox (garbled) | ✓ | LocalControl | Unknown? | toggle 检查可能不覆盖 checkbox type | capability | SettingsSemanticCapability | 可用 LocalControl | pattern check |
| Settings (seq 2 only) | ✓ | NonInteractive | Unknown? | 可能不在 accepted list | 需确认 | — | — | — |

## 17. F-Class Status

无 F 类（截断行）证据。

## 18. Full Regression

2280 pass / 10 fail（全部预存环境类：4 Adaptive + 1 Capstone + 1 ExternalBoundary + 3 VisionHost + 1 extra Adaptive）

## 19. AuthorityDelta

NONE（故障隔离是行为修复，不是权威变更）

## 20. RuntimeBehaviorDelta

`PER_OCCURRENCE_SEMANTIC_FAULT_CONTAINMENT` — 零宽度 occurrence 不再污染整帧

## 21. Phase26 Reentry Readiness

```
NOT_READY
原因：icon/checkbox 类 Unknown 需要新的 capability pattern（或分类为 NonInteractive）
     —— 这些是 D/E 修复后暴露的下一层，需要新 Human Gate
```

## 22. Remaining Human Gates

1. **ICON_TEXTLESS_PATTERN_GATE**: 无文字图标 → NonInteractive（capability 层新 pattern）
2. **CHECKBOX_GARBLED_TEXT_GATE**: 乱码 checkbox → LocalControl 或 NonInteractive
3. **Phase 2.6 Reentry Gate**: 以上完成后

**Stopped per gate.**
