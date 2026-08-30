# PROJECT_LEADER_PHANTOM_FRAGMENT_ORIGIN_DIAGNOSTIC_RESULT

> Gate: `PROJECT_LEADER_PHANTOM_FRAGMENT_ORIGIN_DIAGNOSTIC_GATE`
> Date: 2026-08-30 · 方式: **DIAGNOSIS ONLY（零代码修改）** · Phase 2.6 STOPPED
> 前置：`UNKNOWN-AFFORDANCE-FRAGMENT-COMPLETENESS-BLOCKER-DIAGNOSTIC-RESULT.md`（已证该 blocker 非
> normalizer 引入，属被揭开的既有下一 blocker；本 gate 继续挖它的**来源**）
> 数据：fresh real r2 真机帧（`/tmp/p26-normalizer-repair-r2-{frames,fusion,stage}.json`，
> `P26_CAPTURE_STAGE_VIEWS=1` —— stageViews 含 rawModelDetections / normalizedDetections /
> fusionStages(9 层) / fusedEvidence(带 evidence 血缘)）+ r1 对照（`p26-normalizer-repair-r1-*`）。

## 0. 一句话

Display child 每帧 6..13 个 `type=NonInteractive, text="", StableKey=null, rowId=null` 的
“phantom fragment”，**不是独立 UI 对象，也不是 detector 噪音**：它们是 fusion `row-relation-head`
合成行 band 时发布的 **satellite**（`relation_head_band_N_sat_K`，`role=control`），几何/OCR 来源是
**真实 YOLO `text_block` 检测**（与同行 menu_item 的 raw detection 是同一枚 —— 甚至已并入 band 的
`allIds`）——同一 raw detection 被**重复发布**：一次并入 band（menu_item），再一次以无文本卫星独立进入
canonical 世界。fusion 输出里卫星**自带父链 `headId`**，但 canonical 发布层将其丢弃 →
每个卫星成了独立的 primary occurrence → 无文本 → 语义无判决 → Unknown → 完整性计数。

- 分类：**B. SUPPORTING_FRAGMENT**（真实 fragment，属于已知 UI 对象，应表达 relation/并入父对象）；
  不存在 A 级“纯合成容器”泄漏（几何来自真实检测），但**发布边界**存在 A 型缺陷：合成中间产物
  （satellite）被当作 canonical world occurrence 发布（`INTERNAL_COMPOSITION_ARTIFACT ==
  CANONICAL_WORLD_OCCURRENCE` 的违约）。
- 无 D（无充分不可交互证明——它们只是无交互证据的文本层检测）、无 C（raw source 均为真实高置信
  text_block 检测）、无 E（同帧明确属于已知行：`headId` + 空间包含）。
- Safety/OCR 行（`Safety & emergency`、`LOu`）为**另一类**（fusion 组合缺失 / OCR 质量），按 Leader
  指示 DEFERRED，单独登记（§9），不混入本分类。

## 1. Fragment Origin Table（3 个代表 + 全集对照，seq7 accepted 帧）

| canonical idx | fragment id | raw source | raw label/conf | 合成 producer | synthetic output | parent/row relation | 与邻近元素 | 交互证据 | row_id 为空原因 |
|---|---|---|---|---|---|---|---|---|---|
| 16 | `relation_head_band_3_sat_0` | det_12 | text_block/Text @0.80 | row-relation-head（composition-output 层） | 是（satellite 角色） | `headId=relation_head_band_3`（‘Brightness level’，row_012）；**det_12 ∈ band_3.allIds** | 同帧 candidate_4 tb = 同 bounds 同 raw 源；bounds 与 band_3/row_012 行带重合 | 无（raw=Text；structured 侧同行为 clickable 行） | 无文本 → 稳定器无法匹配已知行（X-Known-Rows 按文本+位置） |
| 24 | `relation_head_band_6_sat_0` | det_13 | text_block/Text @0.73 | row-relation-head | 是 | `headId=relation_head_band_6`（‘Screen timeout’）；det_13 ∈ band_6.allIds | candidate_11 tb 同源同 bounds | 无 | 同上 |
| 29 | `relation_head_band_8_sat_1` | det_14 | text_block/Text @0.71 | row-relation-head | 是 | `headId=relation_head_band_8`（‘Dark theme’行带）；det_14 ∈ band_8.allIds | candidate_16 tb 同源同 bounds | 无 | 同上 |
| （其余）21/22/25 及 r1 root idx15/18 | band_5_sat_1/sat_2、band_6_sat_1、band_1_sat_1、band_11_sat_1 | det_16/det_11/det_19/det_6/det_4 | text_block/Text @0.45..0.89 | 同上 | 是 | headId 指向各自行 band；raw 源均 ∈ band.allIds | 同帧同源 tb 副本 | 无 | 同上 |

全集观察：seq7 的 9 个 satellite 中 6 个无文本（→ idx16/21/22/24/25/29），3 个带文本（
`83%`/`Lock screen`/`Will never…` → idx17/20/28，均 NonInteractive）；accepted 帧 7/10/13/16
卫星计数 6/5/13/13（随滚动新行新增）——**settle-frame 持久存在**。

## 2. Raw → Canonical Causal Trace（一个代表 fragment 的完整链）

```
raw YOLO det_12  label=text_block/Text conf=0.80  boundsPx [66,796,432,896]   ← 真实检测（非噪音）
  → normalizedDetections（PREPROCESSED，除归一化外不变）
  → fusion operator 链：
      uniform-list-row-grouping  noop（cadence 不可推断，19 title ids）
      row-relation-head          ACTIVATED（合成行带）：
          带头 band_3  ← det_15（menu_item，row_id=row_012，allIds=[det_15,ocr_4,ocr_3,det_12]）
          satellite band_3_sat_0 ← det_12（NonInteractive，role=control，text=""，headId=band_3）  ← 此处产生
      spacing-verifier / text-relation-check / structured-corroboration  （卫星存活）
  → fusionStages：composition-output(39) → …(prune/prune-text/same-line-dedup 39→35) →
      text-box-misattribution(35→33) → row-stabilization(33)   （9 个卫星全部存活到终点）
  → fusedEvidence（canonical 输入，33 项 = Observation.Elements）
      satellite 记录：type=NonInteractive, role=control, text="", row_id=null, evidence.headId=band_3
  → C# canonical occurrence（SourceGroundingNormalizer 遍历全部 Elements，每项一个 primary occurrence，
      eligible=true；headId/父链丢失）
  → SettingsSemanticCapability：无 RawText → 所有文本 pattern（root/search/parent-return/Pattern-5/
      LooksLikePreferenceRow/Pattern-7）全部落空 → 无 verdict
  → InteractionAffordanceAnalyzer：无 admitted evidence → Unknown（eligible）
  → completeness inventory：Unknown + 无 StableKey + 无 prior menu_item 证据 → bypass 结构性不可用
      → unknownCount++ → “Unknown interaction affordances remain”
```

## 3. A/B/C/D/E/F 分类

| fragment 类 | 判定 | 依据 |
|---|---|---|
| 全部 no-text 卫星（band_*_sat_*） | **B. SUPPORTING_FRAGMENT** | raw 源=真实 text_block 检测（非噪音→非 C）；有 `headId` 父行 + 同帧同行（→非 E）；无独立交互证据且从未被证明不可交互（→非 D）；几何非合成容器（→非 A 对象本体）——但 **canonical 发布**把“合成中间产物当世界对象”是 A 型发布边界违约 |
| 无（本观测集内） | A 纯合成容器泄漏 / C 检测器伪影 / D 可证明不可交互 / E 真交互 | 无证据支持 |
| 全集 | **B（同构）**，非 F | 6..13 个碎片全部同一 producer（row-relation-head satellite） |

## 4. Exact FDP

**canonical 发布层丢弃 fusion 输出中卫星的父链（`headId`）与“raw 已并入 band.allIds”的事实**：
fusion 已显式记录 satellite→band（`headId`）且 det_12/13/14/16/11/19 同时存在于 band 的
`allIds`（同一物理文本被行带消费），但发布到 C# `Observation.Elements` 时既无 headId 也无
row_id 继承 → `SourceGroundingNormalizer` 把每个 satellite 当作**独立 primary occurrence** →
（无文本 → 语义无判决 → Unknown）→ 完整性计数。第一发散点不在 detector、不在语义 pattern、
而在**融合内部合成产物与 canonical world occurrence 之间的发布边界**。

## 5. Owner / GapKind

- **Owner**：Perception/Fusion **发布边界**（satellite 的 canonical publication）+（次要）C# canonical
  层（无父链承载）。语义层 text-centric 盲区是已知属性，但**本 gate 不修**（Leader: textless→NI NOT
  AUTHORIZED）。
- **GapKind**：`INTERNAL_COMPOSITION_ARTIFACT_AS_WORLD_OCCURRENCE`（fusion 合成中间产物
  satellite 发布为独立世界对象；同 raw 检测重复发布：band.allIds 内 + 独立 occurrence）。
- 不触碰：completeness fail-closed（KEEP）、`Safety & emergency`/OCR（DEFERRED）、OCR/Pattern-5/
  Unknown semantics（NOT AUTHORIZED）。

## 6. 为什么 fragment 进入 independent inventory（机制链）

1. fusion `row-relation-head` 将行带拆为 head（menu_item）+ satellites（NonInteractive，仅内部携带
   行带次要内容）；satellite 的 raw 源同时被 band 消费（allIds）——**同一 raw 检测出现两次**；
2. 发布到 canonical 时 33 项全量进入 `Observation.Elements`（无过滤、无父链、row_id 仅给带头）；
3. `SourceGroundingNormalizer.Normalize` 对**每个** Elements 项生成 primary canonical occurrence
   （eligibility 仅看渠道，不看文本/交互证据）；
4. 语义层对无文本 occurrence 无 pattern 可命中 → 无 verdict → analyzer 回落 Unknown(eligible)；
5. 完整性 Unknown-bypass 需要 StableKey/prior 证据 → 无文本、无行 id 的卫星结构性不可绕过 →
   fail-closed 计数。

## 7. 可复用 relation/composition primitive

1. **fusion 输出内已存在**：satellite 记录的 `evidence.headId`（→其父 band / 行 row_id row_012 等）与
   `typeInferred=row_relation_head_satellite`；band 的 `allIds`（raw 已消费证据）。
2. **C# 语义层已有**：`SemanticComposition.TryVerifyChild` + `ContainerRelationCandidateEvidence`(
   RelationKind.Child, “settings.parent-container”) —— 通用容器→子 relation 通道。当前对 satellite
   不触发是因其 bounds（更高）不被 band（更矮）包含；若发布时携带 headId，可直接表达
   `SupportingOf(menu_item)`。
3. 行稳定器（X-Known-Rows，文本+位置 band → row_id）：卫星无文本 → 不指派（这也解释了 row_id=null）。

## 8. Minimal Repair Candidate（NOT AUTHORIZED，供下一 gate）

依 Leader ruling（B → 复用 generic relation；A → Fusion publication boundary）：
- **首选（publication boundary）**：fusion 发布时**不把 satellite 作为独立 canonical occurrence 发布**
  —— 其 raw 内容已被 band 消费（allIds），卫星是行带内部的 composition artifact，应表达为
  band 的内部结构而非世界对象；发布边界不变量
  `INTERNAL_COMPOSITION_ARTIFACT != CANONICAL_WORLD_OCCURRENCE` 落为代码级检查
  （satellite/band-internal 产物不得进入 Observation.Elements 顶层）。
- **次选（relation）**：若需保留卫星可见性，发布时携带 `headId` 父链 → C# 语义层以
  `ChildOf/SupportingOf(menu_item)` 表达 → suppress 而不改 textless 规则、不改 completeness。
- 两者都**不改**：Textless→NI 规则、no-clickable→NI、completeness bypass、OCR/Pattern-5/Unknown 语义。

## 9. Safety / OCR Blockers（单独登记，DEFERRED，非本分类）

| blocker | 现象 | Owner/方向 |
|---|---|---|
| `Safety & emergency`（r1 root seq13/14） | 真实菜单行在滚动帧只有 text_block、menu_item 仅在后帧(15/16+)合成 | fusion 组合行稳定性（relation-head 对滚动子页，与 C4 veto 家族同源）→ 单独 Fusion gate |
| `LOu`（r1 root seq8） | 真实行 OCR 残块，主通道无判决且被 Admission 为候选 | OCR 质量 / 语义 Admission → 单独 gate |

## 10. Phase 2.6 Next Gate

- **提交候选**：`FUSION_PUBLICATION_BOUNDARY_REPAIR_GATE`（satellite 不发布为独立 canonical
  occurrence；或带 headId 发布转为 SupportingOf relation）。
- 本 gate 零代码修改；Phase 2.6 **维持 STOPPED**；修复 gate 需 Human 授权。

## 11. 边界声明

零代码修改；未改 completeness / OCR / Pattern-5 / Unknown 语义；未按 overlap/bounds 删除；
未放宽任何 fail-closed。全部结论挂 r1/r2 true-stage（stageViews raw→fused）
+ fusion operatorTrace + canonical/semantic 源码行号（`SettingsSemanticCapability.cs` L309-339、
`Agent.OpenWorld.cs` L1241-1330、`SourceGroundingNormalizer.cs` L55-78）。