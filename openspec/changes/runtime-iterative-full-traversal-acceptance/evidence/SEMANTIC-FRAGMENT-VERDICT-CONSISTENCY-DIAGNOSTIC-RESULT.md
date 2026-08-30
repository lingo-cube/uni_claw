# PROJECT_LEADER_SEMANTIC_FRAGMENT_VERDICT_CONSISTENCY_DIAGNOSTIC_RESULT

> Gate: `PROJECT_LEADER_SEMANTIC_FRAGMENT_VERDICT_CONSISTENCY_DIAGNOSTIC_GATE`
> Date: 2026-08-30 · 方式: **DIAGNOSIS ONLY（零代码修改）** · Phase 2.6 STOPPED
> 数据: fresh real r4 accepted Display-child 帧（`/tmp/p26-normalizer-pubfix-r4-*`，seq10/13/16）+
> **语义重放器**（`/tmp/p26-semantic-replay`，对 r4 seq10 重建 Observation→Project→逐谓词矩阵→capability envelopes，
> 与生产代码逐行复刻）——本诊断的 Pattern-5 结论由重放器硬证据锁定。

## 0. 一句话

Display child 中同行 text_block 出现 Unknown/NavigationCandidate/NonInteractive 的"不稳定判定"，
**不是 Pattern-5 条件过宽或优先级问题，而是 Pattern-5 在 V2 事实粒度下结构性永远不生效**：
`IsDuplicatePrimaryRowRendering` 的 peer 查询要求**同一个 fact 同时携带 RawText + Bounds +
menu_item provider**，而 V2 projector（`SemanticObservationFactProjector.AddVisionFacts`）把三者拆分到
**三个独立 fact**（Text fact 只有 rawText+provider；ClassName fact；Geometry fact 只有 bounds）——
单 fact 永远无法同时满足三条 → `peers==0` → **Pattern-5 从未对任何实帧触发**。
同行 text_block 的最终判定于是退化为：有 XML corroboration → NavigationCandidate（
'Brightness level'/'Lock screen'/'Screen timeout'/'Display size and text'）；无 corroboration →
Unknown（'Brightness'/'Lock display'/'Appearance'/'Color'/…）；'83%' 的 NonInteractive 是 **Pattern-7
（副标题几何），非 duplicate suppression**。分类 **A. FACT_FRAGMENTATION（主）+ C.
REPRESENTATION_ROLE_AMBIGUITY（次）**——系统没有"谁是 primary 谁是 secondary"的声明，把鉴别
全部押在"猜 duplicate"谓词上，而该谓词因事实粒度断裂从未兑现。

## 1. Debug IR

```
ExpectedReality: 同一 accepted 帧内，一行已存在 authoritative primary（menu_item、Nav）时，
                 它的 secondary text 表示应得到稳定一致的 composition verdict（supporting/duplicate），
                 而不是独立 Unknown/Nav 义务。
ObservedReality: r4 child seq10/13/16：8/7/7 个 eligible Unknown（Brightness/Lock display/Not set/
                 Appearance/Will never/Color/Color contrast/Other display controls — 全部带 row_id
                 text_block，多数同帧同行有 menu_item peer）+ 4 个被提升为 NavigationCandidate 的
                 text_block（Brightness level/Lock screen/Screen timeout/Display size and text）+
                 completeness 因 Unknown 保持 fail-closed。
TargetOccurrence: provider=text_block、同帧同行（row_id/文本/bounds）存在 menu_item peer 的 occurrence。
GoodComparison: '83%' → NonInteractive：**由 Pattern-7（副标题：位于已分类行下方、同列、gap≤0.6h）命中**，
                与 duplicate/supporting 规则无关；'Not set' r2 曾 NonInteractive（P7 几何不同帧成立）、
                r4 Unknown（gap=0.01125 vs maxGap=0.0105，差 0.00075 越界）——跨帧几何敏感。
BadComparison: BAD_UNKNOWN vs BAD_OTHER 的**唯一事实差异 = structured（XML）同行文本是否出现**
                （'Brightness level'/'Lock screen'/'Screen timeout'/'Display size and text' 在 XML 有
                clickable 行 → corroboration → Nav；'Brightness'/'Lock display'/'Appearance'/'Color'/
                'Color contrast'/'Other display controls'/'Not set'/'Will never' 无 XML 行 → Unknown）；
                Pattern-5 对两者都未触发（重放器逐行证实 peers=0）。
EvidenceChain:
  fusion candidates（tb 与 mi 的 yoloId/ocrId 相同 —— band.allIds 已消费同一 raw 检测）
    → canonical occurrence（两枚都带 StableKey=row_011 等；tb 与 mi bounds 完全一致）
    → Project（每 occurrence 拆成 Text / ClassName / Geometry 三个独立 primary fact；
         struct 行无 bounds、无 class → 单 Text fact）
    → Pattern-5 peer 查询（单 fact 需同文本+bounds+provider → 恒 0）
    → P5 恒 false → LooksLikePreferenceRow(仅 facts)=false（text_block provider/class 不符）
    → 有 corroboration(IsNavigationRowShape: XML clickable+LinearLayout) ? NavigationCandidate : Unknown
    → '83%'/'Not set' 类经 Pattern-7（几何副标题）→ NonInteractive 或越界 Unknown
LastGood: 单元测试 `Overlapping_same_text_primary_box_is_nointeractive_duplicate_of_unique_menu_item`
          （stub fact 单 fact 携带 rawText+provider+bounds，P5 命中 → NonInteractive）
FirstBad: **生产 projector 的 fact 拆分粒度 ≠ Pattern-5 的"单 fact 同文本+bounds+provider"输入假设**
          —— P5 在 V2 管线对任意实帧均不命中（重放器 14 个 text_block 全 peers=0）。
GapKind: A. FACT_FRAGMENTATION（谓词输入粒度与事实输出不一致）+ C. REPRESENTATION_ROLE_AMBIGUITY
         （无 primary/secondary 角色声明，secondary 判定依赖偶然的 XML corroboration）。
Owner: SettingsSemanticCapability（`IsDuplicatePrimaryRowRendering` 的输入聚合粒度）× 生产事实投影
        （`SemanticObservationFactProjector` 的 fact 拆分）—— **语义/composition 权属，非 completeness**。
EvidenceRefs: 本 gate 语义重放器（/tmp/p26-semantic-replay，输出矩阵全量）；r4 stage/frames
              seq10/13/16；`SettingsSemanticCapability.cs` L316-339（P5）、L144（corroboration Nav）、
              L158-217（P7）、L236-259（Correlate）、L296-301（IsNavigationRowShape）；
              `SemanticObservationFactProjector.cs` L52-71（fact 拆分）；
              `ExternalSettingsSemanticCapabilityTests.cs` L126-163（stub 单 fact 盲区）。
MissingEvidence: 无阻断性缺口（facts 已由重放器证明）。附带观察：campaign 的 structured 行不携带
                 bounds（XML 几何未传），corroboration 仅文本相等，加剧 Nav/Unknown 分裂的偶然性。
Disposition: **FDP + Owner 已充分 → propose MINIMAL_REPAIR_GATE**（谓词输入粒度修复，
             见 §6；无需新 composition authority，无需 contract 变更）。
```

## 2. Good / Bad Row Differential（3+ 组，r4 seq10 实帧）

| 组 | row | tb verdict | mi? | XML 同行? | Pattern-5 peerCnt(重放) | 判据 | 键轴 |
|---|---|---|---|---|---|---|---|
| GOOD(非P5) | row_024 '83%' | NonInteractive | 无 | 无 | 0 | Pattern-7 副标题（'Brightness level' 下方 gap 0.0062≤0.0127） | P7 几何 |
| BAD_UNKNOWN | row_011 Brightness | **Unknown** | mi idx16 同文本同bounds | 无 | **0** | P5 死 + 无 corroboration + P7 不满足 | P5 断裂 |
| BAD_UNKNOWN | row_013 Lock display | **Unknown** | mi idx18 同文本同bounds | 无 | **0** | 同上 | P5 断裂 |
| BAD_UNKNOWN | row_017 Appearance | **Unknown** | mi idx21 同文本同bounds | 无 | **0** | 同上 | P5 断裂 |
| BAD_UNKNOWN | row_020 Color | **Unknown** | mi idx24 同文本同bounds | 无 | **0** | 同上 | P5 断裂 |
| BAD_UNKNOWN | row_025 Color contrast | **Unknown** | mi idx26 同文本同bounds | 无 | **0** | 同上 | P5 断裂 |
| BAD_UNKNOWN | row_016 Not set / row_018 Will never | Unknown | **无 mi** | 无 | 0 | P5 死 + P7 越界（gap 0.01125>0.0105） | P5 断裂+几何 |
| BAD_OTHER | row_012 Brightness level | NavigationCandidate | mi idx17 同文本同bounds | **有** | 0 | P5 死 + XML corroboration → Nav | corroboration |
| BAD_OTHER | row_014 Lock screen / row_015 Screen timeout / row_019 Display size | NavigationCandidate | mi 同文本同bounds | **有** | 0 | 同上 | corroboration |

UNCHANGED：mi → NavigationCandidate（自持 provider menu_item，LooksLikePreferenceRow）——恒定。
CHANGED 轴：tb 的 corroboration 可用性（XML 捕获偶发）+ P7 几何窗口（跨帧）——两者都掩盖了
"相同同行 tb 的 input 条件其实完全相同"这一事实。
FIRST_SEMANTICALLY_RELEVANT_CHANGE：**Pattern-5 peer 查询在 projector 拆分事实下恒 0 命中**（重放器逐行）。

## 3. Pattern-5 Predicate Matrix（IsDuplicatePrimaryRowRendering，L316-339 逐项）

| 谓词轴 | 单元测试 stub（P5 命中✅） | 生产 V2 fact（P5 恒 0❌） |
|---|---|---|
| 同行同文本（RawText == text） | stub 单 fact 携带 → ✅ | 仅在 Text fact；✅是 |
| bounds 重叠（f.Bounds + Overlaps） | stub 单 fact 携带 → ✅ | **仅 Geometry fact**；❌ 不满足 |
| provider ∈ {menu_item} | stub 单 fact 携带 → ✅ | **仅 Text fact**；✅ |
| **同 fact 同时满足三者** | **✅（单 fact）** | **❌ 结构性不可能（Text/Class/Geom 三 fact 分离）** |
| peer 计数 == 1 | ✅ | **恒 0** → false |

结论：谓词"本身不宽也不窄"——它**根本没被输入**；修复方向 = 聚合粒度（按 OccurrenceId 组合
  事实后再判定），非放宽条件。

## 4. Human Reality Check

对每个 BAD 行：人在屏幕上看到**一个 row 的多个视觉表示**（menu_item 行 + 其文字层 text_block），
不是两个独立交互对象。`SECONDARY_REPRESENTATION != INDEPENDENT_INTERACTION_OBLIGATION`。
同文本 / bounds 重叠 / row_id 之一单独看不能证明 duplicate（gate 冻结不变量），但本 gate 已具备
**更强的同源证据**（fusion 层 band.allIds 与 tb.yoloId 同一 raw 检测；canonical 层双 occurrence
共享同一 StableKey=row_011 + 全同 bounds）——不是靠单项猜。

## 5. 现有 generic primitive 是否足够 / 是否需架构

- **可复用（已存在）**：① fusion `evidence.allIds`（tb 与 band 同 raw 检测——`candidate_N.yoloId ==
  relation_head_band_K.yoloId`，r2/r4 provenance 证实）；② canonical 层 `StableKey/row_id`（tb 与 mi
  共享 row_011）；③ capability 内已有按 OccurrenceId 的事实分组结构（L167 主循环）。
- 判断：**无需新 composition authority / 无需契约变更**——问题在既有 Pattern-5 的**输入聚合粒度**，
  把 peer 判定改为 occurrence 聚合后（同组内分别取 text/geometry/provider），Pattern-5 即可命中
  真实同行；无需求新关系（fusion 的 allIds 证据可作为未来显式 relation 的备料，但非本 gate）。
- 因此 **Disposition = MINIMAL_REPAIR_GATE**（不涉及 completeness / Unknown fail-closed /
  textless fallback / Pattern-5 放宽）。

## 6. Minimal Repair Candidate（NOT AUTHORIZED，供下一 gate）

在 `SettingsSemanticCapability` 内（仅此一层）：

1. **predicate 输入聚合**：`IsDuplicatePrimaryRowRendering` 的 peers 判定改为**按 OccurrenceId 聚合
   primaryFacts 后再取**（occurrence 组内 Text/Geometry/provider 各就各位），peer 条件 =
   存在且仅存在一个**其他 occurrence 组**满足（组内文本 == current 文本、组内 bounds 与 current
   bounds 重叠、组内 provider ∈ menu_item）——与 L167 主循环同一分组维度；
2. **无任何放宽**：same-text/bounds/provider 之外的判据（文本空、bounds 空、多 peer 歧义）保持
   fail-closed；
3. **fixture 盲区封堵**：`ExternalSettingsSemanticCapabilityTests` 相应 stub 改为 **projector 真实
   粒度**（Text/ClassName/Geometry 三 fact 组），使单测与生产输入同构（RED→GREEN：当前 stub 粒度
   下 P5 死 → 生产粒度下 P5 命中判同）；
4. 不动：completeness（Unknown fail-closed / IsPhysicalRowDuplicate / knownStableKeys / prior-repeat
   bypass）、`InteractionAffordanceAnalyzer`、`SourceGroundingNormalizer`、fusion、OCR/Pattern-7/
   icon/Safety。不引入 text-block→NonInteractive fallback、不做 primary/secondary 硬编码。

预期（候选修复的验收，供下一 gate 自证）：r4 seq10 'Brightness'/'Lock display'/'Appearance'/'Color'/
'Color contrast'/'Other display controls'（有同行 mi）→ NonInteractive（duplicate suppression）；
Nav 提升类（Brightness level 等）→ 同为 NonInteractive（不再双重 source）；仅无同行 peer 的
'Not set'/'Will never' 类保持现状（各层 fail-closed 不变）；child 完整性 Unknown 义务下降并以
genuine 项为准。

## 7. Phase 2.6 Next Gate（Human）

- **提交候选**：`PATTERN_5_OCCURRENCE_GRANULARITY_REPAIR_GATE`（§6 最小修复 + 同构单测 RED→GREEN
  + 全量确定性套件 + fresh real Display child 复验 deltas）。
- 本 gate 零代码修改；Phase 2.6 **维持 STOPPED**；完成后停止。

## 8. Boundary Declaration

零代码修改；未放宽 Pattern-5 / 未新增 textless fallback / 未用 same-row 短路 / 未动 completeness
（Unknown fail-closed、IsPhysicalRowDuplicate、knownStableKeys、prior-repeat bypass）/ 未修 icon/OCR/
Safety；未改 Fusion publication repair。全部结论挂重放器输出 + 实帧 stage + 源码行号。