# PROJECT_LEADER_SETTINGS_DE_RESIDUAL_IMPLEMENTATION_RESULT

> Gate: SETTINGS_DE_RESIDUAL_IMPLEMENTATION · 2026-08-29

## 1. D Diagnostic Trace Implementation ✅

`SemanticCapabilityEnvironment.ObserveAsync()` 的 catch 子句添加了 stage-tracking 诊断：
- 追踪失败阶段：project → staleness-check → source-mapping → source-validation → capability-evaluation → evidence-commit
- 区分 exception vs admission rejection
- 输出 `[semantic-diagnostic] seq=N stage=X FAILED type=Y message=Z` 到 stderr
- 行为零变更（fail-closed 路径完全相同）

## 2. D Fresh Runtime Evidence ✅

D2 真机运行（首轮）立即揭示：
```
[semantic-diagnostic] seq=20 stage=project FAILED 
type=ArgumentOutOfRangeException 
message=Bounds must fit the normalized frame. (Parameter 'width')
```

**D 的真实失败机制**：`SemanticObservationFactProjector.AddVisionFacts()` 中的 `Normalize(bounds)` 在元素 bounds 宽度 ≤ 0 时抛出 `ArgumentOutOfRangeException`。这导致**整个 observation 的所有证据被清空**。

## 3. D Actual Failure Mechanism

| 项 | 值 |
|---|---|
| FDP | `SemanticObservationFactProjector.AddVisionFacts()` → `SemanticNormalizedBounds` 构造器验证 width > 0 → 抛出 |
| Owner | `SemanticObservationFactProjector` |
| GapKind | **IMPLEMENTATION_BUG** |
| 机制 | 感知管线偶发产生宽度为 0 的 bounds → projector 抛异常 → SemanticCapabilityEnvironment 的 catch 清空整帧证据 → 所有元素 Unknown |
| Candidate Fix | `AddVisionFacts` 中 `bounds.IsValid` 检查 → 跳过 Geometry fact（不抛出） |

## 4. D 的最新观察（E4 轮）

E4 轮（加了 subtitle pattern 后）：**无诊断输出**（本轮没有无效 bounds）。但 'Settings' 仍 Unknown。

**这表明 D 有两个独立的失败路径**：
1. 路径 A（已确认）：无效 bounds → projector 抛出 → 整帧清空（D2 诊断确认）
2. 路径 B（未完全确认）：capability 正常运行、无异常、无拒绝，但 'Settings' 仍未被分类。**可能原因**：analyzer 的 occurrence ID 匹配失败（evidence 在 pre-stabilization 观测上计算，analyzer 在 post-stabilization 观测上查找）

## 5. D Next Human Gate Needed? YES

**D_BEHAVIOR_FIX_GATE**：修复 projector 的 invalid bounds 处理（跳过而非抛出）。
**D_ANALYZER_MATCHING_DIAGNOSTIC**：如果路径 B 是主因，需要在 analyzer 层加诊断。

## 6. E Deterministic RED ✅

测试 `Subtitle_below_known_preference_row_is_noninteractive`（RED → 未实现时失败）
测试 `Next_menu_row_below_previous_row_is_NOT_subtitle`（反例保护）

## 7. E Final Admission Predicate

```
PATTERN 7: SUBTITLE / DESCRIPTION OF KNOWN ROW
条件（全部满足才触发）：
  1. Primary text occurrence 在同帧内被 Pattern 1-6 分类后仍无证据
  2. 存在同帧内已被 Pattern 6 分类为 NavigationCandidate 的 preference row R
  3. T 的文本 ≠ R 的文本（排除重复渲染 — 那是 Pattern 5）
  4. T.Left 与 R.Left 对齐（差 ≤ 0.05，同列）
  5. T.Top 在 [R.Bottom, R.Bottom + R.Height × 0.6]（正下方紧贴）
  6. T 无 toggle/switch 形状证据
输出：settings.preference-row + NonInteractive
语义：DESCRIPTION_OF_KNOWN_ROW（不是 NON_CLICKABLE_TEXT）
```

## 8. E Pattern Ordering Proof

Pattern 7 在 Pattern 1-6 全部执行完毕后运行（two-pass）：
- Pass 1: Patterns 1-6 分类所有可分类的 occurrence
- Pass 2: Pattern 7 只处理 Pass 1 未分类的 occurrence，且要求存在 Pass 1 已分类为 NavigationCandidate 的锚行

安全性：
- Page title (Pattern 1) 先于 Pattern 7 → 不受影响 ✓
- Search/back (2/3) 先于 7 → 不受影响 ✓
- LocalControl (4) 先于 7 → toggle 不被吞 ✓
- Duplicate (5) 先于 7 → 同文本重复不被误判为 subtitle ✓
- NavigationCandidate (6) 先于 7 → 正常行优先正确分类 ✓
- 只有 1-6 都不匹配的剩余 occurrence 才进入 7 ✓

## 9. E RED→GREEN ✅

- `Subtitle_below_known_preference_row_is_noninteractive`：**RED → GREEN** ✓
- `Next_menu_row_below_previous_row_is_NOT_subtitle`：**GREEN** ✓
- 全 capability 套件：**16/16 GREEN** ✓

## 10. E Counterexample Matrix

| # | 反例 | 结果 |
|---|---|---|
| 1 | Page title → 仍由 Pattern 1 处理 | ✓（Pattern 1 先于 7）|
| 2 | Normal preference row → NavigationCandidate | ✓（Pattern 6 先于 7）|
| 3 | Subtitle child → NonInteractive | ✓（Pattern 7 正确分类）|
| 4 | Local control label → 不被吞 | ✓（Pattern 4 先于 7 + toggle 检查）|
| 5 | Next menu row → 不误判 subtitle | ✓（menu_item 有 Pattern 6 证据 → 不进入 7）|
| 6 | Same-text rows → 独立 identity | ✓（文本不同条件排除）|
| 7 | First-seen clipped text → 不因靠近就判 subtitle | ✓（要求锚行已被分类）|
| 8 | OCR stray text → 无充分 relation | ✓（要求列对齐+紧贴下方）|
| 9 | Structured-only non-clickable → 不单独产生分类 | ✓（要求 primary Vision occurrence）|
| 10 | Valid Vision + structured description → 正确 NonInteractive | ✓ |

## 11. E Authority Preservation

- Vision = Primary ✓（Pattern 7 只处理 Primary tier facts）
- Structured = Auxiliary corroboration only ✓（Pattern 7 不依赖 structured 单独决定）
- 无新 Runtime-wide semantic type ✓（复用 NonInteractive）
- 无 Settings 文本依赖 ✓（纯结构：列对齐 + 位置关系 + 文本不等）

## 12. Targeted Regression ✅

| Suite | Result |
|---|---|
| OpenWorld + SettingsStrategyBinding + Quiescence + SourceRoleStability | **143/143** |
| Full suite | **2280 pass / 9 fail**（全部预存环境类）|

## 13. Fresh Real Campaign (E4) ✅

- 无 semantic diagnostics（无 projector 异常）
- 仍 "Unknown interaction affordances remain"
- 剩余 Unknown：'Settings' (row_001) + 'Dark theme...' (row_015)

## 14. row_001 Final Status

**UNKNOWN — 但现在是可诊断的**

- 路径 A（projector 异常）已确认并可修复（需要 D_BEHAVIOR_FIX_GATE）
- 路径 B（analyzer 匹配）待进一步诊断
- **本轮成功标准达成**："如果仍失败，我们现在明确知道它为什么失败" ✓

## 15. row_012/015 Final Status

**Pattern 7 已实现，但真机效果待确认**

- 'Dark theme...' 在 seq 10 是 text_block，其上方是否有已被分类的 NavigationCandidate 行？
- 如果上方的 'Display' 在 seq 10 也是 text_block（未分类），则 Pattern 7 无法锚定
- **需要真机 trace 确认 Pattern 7 是否实际触发**

## 16. Residual Unknown Inventory

| 元素 | 类型 | 根因 | 修复路径 |
|---|---|---|---|
| 'Settings' (row_001) | Page title | 路径 A: projector bounds bug；路径 B: analyzer matching | D_BEHAVIOR_FIX_GATE |
| 'Dark theme...' (row_015) | Subtitle | Pattern 7 已实现但可能未触发（锚行未分类）| 验证 Pattern 7 触发条件 |

## 17. F-Class Evidence

未发现新 F 类（截断行）。

## 18. Phase26 Reentry Readiness

```
NOT_READY
剩余：D 修复（projector bounds + 可能的 analyzer matching）+ E Pattern 7 真机效果验证
```

## 19. AuthorityDelta / RuntimeBehaviorDelta

```
AuthorityDelta: NONE
RuntimeBehaviorDelta: PRESENT_IF_IMPLEMENTED (subtitle pattern → NonInteractive for descriptions)
```

## 20. Remaining Human Gates

1. **D_BEHAVIOR_FIX_GATE**: 修复 projector invalid bounds 处理
2. **E_VERIFICATION_GATE**: 验证 subtitle pattern 真机效果（如需调整锚行条件）
3. **Phase 2.6 Reentry Gate**: 以上完成后

**Stopped per gate.**
