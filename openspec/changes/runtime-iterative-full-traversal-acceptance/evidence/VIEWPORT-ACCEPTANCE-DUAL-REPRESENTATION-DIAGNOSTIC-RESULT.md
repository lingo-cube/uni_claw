# PROJECT_LEADER_VIEWPORT_ACCEPTANCE_DUAL_REPRESENTATION_DIAGNOSTIC_RESULT

> Gate: `PROJECT_LEADER_VIEWPORT_ACCEPTANCE_DUAL_REPRESENTATION_DIAGNOSTIC_GATE` · 2026-08-29
> Decision: Frame-local fusion repair ACCEPTED · Phase 2.6 STOPPED · 诊断 APPROVED · acceptance/dedupe 行为 change NOT AUTHORIZED
> 方式: **DIAGNOSIS ONLY**（零代码修改）。目标 run: `/tmp/p26-repair-POST3-*`（fresh real campaign，6 帧/6 trace，
> terminal = `Failed — quiescence admission budget exhausted (last seq=9, attempts=3, multiplicity mismatch …)`）

## 0. 结论速览

**budget exhaustion 的直接原因不是双表示（menu_item + text_block 同帧同 row）的重复 admission，而是
`spacing-verifier` 在子页第二次滚动帧（seq 9）上对整组组合行 **column-spread C4 veto** 导致的
导航投影计数塌缩（11 → 1），引发 (7,9) multiplicity mismatch → 预算耗尽。**

按 gate 分类表：双表示本身为 **B（SUPPORTING_CHILD / repeat representation）**，但**不是 exhaustion 的直接
原因**；exhaustion 归 **D（由其他 admission oscillation / 组合有效性 veto 导致）**。依据 gate 指令
（"如果是 D → 转向真实 blocker，不修 acceptance"）：

- **不提交** `VIEWPORT_ACCEPTANCE_LOGICAL_SOURCE_RECONCILIATION_REPAIR_GATE`；
- 真实 blocker 在 **Fusion 层**（spacing-verifier C4 全组 veto 与 relation-head 子页滚动组合的交互）——
  见 §7 下一 gate 候选。

## 1. Exact Causal Trace（真机帧级，融合 trace + 帧投影 + 源码谓词重建）

### 帧序列（POST3，tap 记录）
`seq 1/2` root · `seq 4` root（首滚后）· `seq 6/7` **Display 子页**（导航后，11 menu_item）· `seq 9` 子页二滚帧。

### navigation-source 投影（= `NavigationRowCenters`：NavigationCandidate 且 primary-vision 且 bounds 有效；
signature=`rowId|menu_item|""|""`；multiplicity-preserving，无 dedupe）

| 帧对 | rowsA→rowsB | IsViewportStable |
|---|---|---|
| (1,2) | 8→8 同签名同中心 | **STABLE** |
| (2,4) | 8→8 | **STABLE** |
| (4,6) | 8→11（root→Display 子页导航）| CountMismatch（导航边界）|
| (6,7) | **11→11 同签名同中心（漂移=0）** | **STABLE** |
| **(7,9)** | **11→1** | **CountMismatch → last classification → budget exhausted** |

⇒ 消耗预算的正是 **(7,9)**：seq-7 的 11 个 menu_item vs seq-9 的 1 个 menu_item（`'ispiay'` row_025）。

### seq-9 组合为何塌缩（融合 trace 直接读出）
```
step0 uniform-list          noop | cadence model not inferable            menuItemIds=[candidate_1]
step1 row-relation-head     activated | merged 11 band head(s), suppressed 1   menuItemIds=[candidate_1, band_1..band_12]
step2 spacing-verifier      **fail_closed | "generated rows' column spread 95px exceeds the tolerance bound 42.4px (median step…)"**
                            ⇒ 执行器回滚（fail-closed rollback）→ menuItemIds 回到 [candidate_1]
FusionOutput: menu_items=[('candidate_1','ispiay')]  text_blocks=15（含 'Appearance' x2, 'Other display controls' x2 …）
```
- 谓词：`spacing_verifier.verify` C4 column-spread —— `spread = max(x1)-min(x1)` over generated bands =
  **95px > tolerance = max(24, 2·0.20·median_gap) = 42.4px** → veto（`operators/spacing_verifier.py` L291-…）。
- 触发数据：子页二滚视口内组合行 x1 离散（含顶部边界 garbled band `'ispiay'` 与
  `'Color contrast'`/`'Other display controls'` 等不同 x1 行），且 **topmost-title 豁免未命中**
  （豁免仅限"单一 far-left title + 单一 dominant 列簇"形状，本帧非该形状）→ 原全组 spread 检查否决。
- 结果：**整组组合被回滚** → 该帧只剩 1 个导航源 → (7,9) multiplicity mismatch。

## 2. Offending Occurrence Pairs

| 消耗点 | offending |
|---|---|
| (7,9) 帧对 | seq-7 11 源 vs seq-9 1 源（`nav:0 'ispiay' row_025`）——被 veto 回滚掉的是
  `relation_head_band_1..12`（Display/Brightness/Lock display/Lock screen/Screen timeout/Appearance/
  Dark theme/Display size and text/Color/Colors/…）|
| 帧内 | 无重复签名（全部帧 dup_sigs=0）——**不存在同帧重复 logical source** |

## 3. Logical vs Physical Representation Analysis（双表示分类）

| 帧 | 物理表示（element 层） | 逻辑 source 层（OccurrencesOf / NavigationRowCenters） | 分类 |
|---|---|---|---|
| seq6/7 每行（Display,Brightness,…,Dark theme,Colors） | text_block(row_0XX) + menu_item(row_0XX) 同帧共存（34 elements / 11 逻辑行） | 仅 menu_item 进入：
  text_block 副本经 `SettingsSemanticCapability` **Pattern 5**（同文本+重叠 bounds 的 menu_item peer）
  → **NonInteractive** → 非 NavigationCandidate → 不入投影 | **B (SUPPORTING_CHILD / repeat representation)**，
  **未重复计入逻辑 source 清单**（`OccurrencesOf` 只收 NavigationCandidate；`EligibleForAuthorization`=PrimaryVision）|
| `'83%'`/`'Not set'`/`'Will never turn on automatically'`/`'Showallnotificationcontent'` | text_block / NonInteractive | NonInteractive | **B**（父行子元素，非独立 source）|
| `'ispiay'`(row_025) | menu_item（顶部边界 OCR 乱码带） | 唯一存活 navigation source（seq9）| **C（TRUE_INDEPENDENT_OCCURRENCE — 垃圾占位源）**（非双表示；是 C4 veto 后残留）|
| `'Appearance'`×2 / `'Other display controls'`×2（seq9） | 同文本 text_block 双份 | 不入投影（非 NavigationCandidate）| **B（引擎噪声/副本）**，不影响投影计数 |
| 帧内同签名重复 | 无 | 无 | **非 A** |

**关键结论**：同一 logical row **未发生重复 admission**（A 类不成立）——逻辑 source 清单每个 row 只计
一次；双表示只在 **element（物理）层**冗余，而 quiescence 比较的是 **导航投影（逻辑层）**，两者不混。

**StableKey/rowId/composition 关联性**：双份共享 `row_id`（row_010 等，稳定器按文本+位置 band 指派）+
重叠 bounds + 同文本——**足以证明关联**（语义 Pattern-5 用"同文本+重叠 bounds"判定重复，rowId 额外证同源）。
rowId 同时用作签名中的身份键（`StableKey ?? Text | PerceptionType`）→ 双份签名不同（type 不同）但非重复源。

## 4. 五问直答

1. **exact 哪些 occurrence 消耗了 budget**：seq-9 的**唯一导航源**（'ispiay' row_025）与 seq-7 的 11 个
   menu_item 的计数差——实际是 seq-9 被 veto 回滚后**消失**的 `relation_head_band_1..12`（12 个本应存在的
   导航源）。消耗发生在 quiescence 的 (7,9) 比较（attempt1→mismatch，attempt2/3 无新帧 → 预算耗尽）。
2. **同一 logical row 重复 admission？** **没有**（A 类不成立；帧内 dup_sigs=0；逻辑 source 每行唯一）。
3. **StableKey/rowId/composition 证明关联？** **是**（同 rowId + 重叠 bounds + 同文本；签名身份=rowId|type）。
4. **residual icon/text_block Unknown 与 budget exhaustion 同一 FDP？** **否**。exhaustion 发生在
   **viewport acceptance 之前**（fail-closed：无不稳定帧被接纳 → 未产生 inventory → RunFailed），
   Unknown 计数/语义覆盖根本没有进入决策路径；两者不同 FDP（接受层 vs 语义覆盖层）。双表示副本
   （text_block NonInteractive）也**不是** Unknown。
5. **FDP / Owner / GapKind**：
   - **FDP**：`spacing_verifier.verify` 的 C4 column-spread veto（95px > 42.4px bound）在 Display 子页
     二滚帧整组回滚 relation-head 组合 → 导航投影塌缩。
   - **Owner**：Perception/Fusion 组合有效性层（spacing-verifier C4 谓词 × relation-head 对滚动子页
     组合的 x1 离散度）。
   - **GapKind**：`FRAME_LOCAL_COMPOSITION_VALIDITY_VETO`（组合有效性帧内否决），**不是** acceptance 逻辑
     缺陷，**不是** 双表示重复 admission。

## 5. 是否需要 production repair

- **Acceptance/dedupe：不需要**（D 类——不修 acceptance，遵守 NOT AUTHORIZED）。
- **Fusion 层：需要（候选）**——真实 blocker 是"relation-head 在滚动子页组合的行集 x1 离散度超过
  spacing-verifier C4 容忍 → 整组回滚"。候选修复方向（待 Leader 授权，**本 gate 不实施**）：
  C4 检查对 relation-head 组合行集与 uniform-list 网格行的**适用边界**（relation-head 组合不承诺
  uniform-list 列栅格——verifier 的推导性论证只对 uniform-list 生成行成立；对 relation-head 行集按
  dominant-column 聚簇或按 band 局部列校验），或 relation-head 侧列对齐约束——均需新 gate 裁决。

## 6. Residual Unknown Inventory

- 本 run（POST3）：**无 admitted frame → 无 inventory → 无 Unknown 计数**（Unresolved before counting；
  终态为 quiescence exhaustion，非 Unknown-affordance 终态）。
- 双表示副本（text_block）：语义层 Pattern-5 → **NonInteractive**（非 Unknown）。
- 既有残余（前序 run 已登记，非本 FDP）：无文字 icon → Unknown（ICON_TEXTLESS_PATTERN_GATE）；
  子页/根页 resolved-语义未覆盖元素等（Phase 2.6 残余清单；需各自 Human Gate）。

## 7. Phase 2.6 Next Gate

- **不提交** `VIEWPORT_ACCEPTANCE_LOGICAL_SOURCE_RECONCILIATION_REPAIR_GATE`（D 类，acceptance 非 blocker）。
- 下一候选 gate：**`FRAME_LOCAL_COMPOSITION_VALIDITY_VETO_REPAIR_GATE`**（spacing-verifier C4 谓词 ×
  relation-head 滚动组合的适用边界）——替代性候选：由 Leader 裁决是否将 C4 校验限定于
  uniform-list 生成行（`evidence.typeInferred` ∈ uniform-list reason set）而不波及 relation-head band。
- Phase 2.6 维持 **STOPPED**。

## 8. 边界声明

- 零代码修改；未 dedupe-by-text；未动 quiescence budget/completeness/Fusion；未用未来帧修正当前帧；
  未顺手修 residual Unknown。全部结论挂真机帧数据 + 融合 trace + 源码谓词（`Agent.OpenWorld.cs`
  `IsViewportStable/NavigationRowCenters/ConfirmScrollStabilityAsync`、`SourceEquivalenceNormalizer.OccurrencesOf`
  `BuildSignature`、`spacing_verifier.verify` C4、`SettingsSemanticCapability` Pattern-5）。