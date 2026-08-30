# UNKNOWN-AFFORDANCE-FRAGMENT-COMPLETENESS-BLOCKER-DIAGNOSTIC_RESULT

> Gate context: 在 `SOURCE_NORMALIZER_LOGICAL_ORDER_RECONCILIATION_REPAIR_GATE` fresh real 复验
> （r1/r2）中被揭示的下一 first blocker：`Unknown interaction affordances remain; completeness cannot be proven.`
> 方式: **DIAGNOSIS ONLY（零代码修改）** · Phase 2.6 STOPPED
> 分析顺序（Leader 指示）: 证据 → 信息分析 → 人操作手机的思维 → 代码定位。

## 0. 一句话

这个 blocker 不是"页面上真有未知交互元素"，而是**感知/语义层把同一屏幕切碎后，部分碎片拿不到语义判决**：
(a) 无文字无 row 的行背景容器碎片（每帧 6..13 个，**结构性无法被任何 pattern 或完整性 bypass 判定**）；
(b) 个别真实菜单行在特定帧只有 text_block 曝光（`Safety & emergency`，其 menu_item 合成只在后帧出现）；
(c) garbled OCR 行（`LOu`）主通道无判决。完整性 gate 对这些授权 Unknown 按 fail-closed 计数 → 阻塞。
**该 blocker 在本 gate 修复前即已存在（被 normalizer Unresolved 掩盖）**——pre-repair run-1 的 child 帧
携带完全相同的 Unknown 集合，只是当时在完整性检查之前就已 Unresolved 终止。

## 1. 证据（fresh real r1/r2 stage 帧 + pre-repair run-1 对照）

| run | 容器 | accepted 帧 | 关键 Unknown（eligible=True） |
|---|---|---|---|
| r1 | root | 5,8,11,14,17,19 | seq8: `LOu`(row_010 tb)、无文字 NI idx15/18；seq14: `Safety & emergency`(row_019 tb，**同帧无 menu_item peer**) |
| r2 | child Display | 7,10,13,16 | 每个 accepted 帧都有：7 个 text_block 副本（Display/Brightness/Lock display/Appearance/Will never/Display size and text/Color → 同帧它们的 menu_item peer **已分类 NavigationCandidate**）+ **6..13 个无文字 NonInteractive 容器碎片**（row=-，无 StableKey） |
| pre-repair run-1 | child | （未达完整性即失败） | seq21/22 携带**同一集合**（154 个 Unknown：row_010 Display tb、row_019 Brightness tb、…、无文字 NI）→ 证明本 blocker **先于本 gate 存在，属被揭开的面具** |

**双渠道澄清**：同一 elementIndex 在 `affordances` 中可同时有 vision（eligible）与 structured
（ineligible）两条记录（如 r2 seq6 idx1 'Display'：vision=Unknown, structured=NavigationCandidate）。
此前按 elementIndex 折叠的 dump 会掩盖 vision 真实判定；以 **eligible-only** 视图为准。
同一帧内 verdict 是**确定性的**（不跨帧翻转）：row_012/014/015 tb → NavigationCandidate（双源污染）、
row_016 tb → NonInteractive（Pattern-7）、row_010/011/013/017/018/019/020 tb + 全部无文字碎片 → Unknown。

## 2. 信息分析（三类结构性 Unknown）

1. **无文字容器碎片**（type=NonInteractive, text="", row_id=-）：r2 child 每帧 6→13 个（随页长增长），
   根帧 idx15/18 同理。它们是 fusion 对行背景/容器合成的无文本元素。
2. **有同行 peer 的 text_block 副本**（r2：Display/Brightness/Lock display/Appearance/Will never/
   Display size and text/Color）：同帧其 menu_item 或 NonInteractive peer 已获判定 → **完整性 bypass
   （`IsPhysicalRowDuplicate`）本可消解** → 不构成计数；但它们是 Pattern-5 抑制失效的表征。
3. **无同行 peer 的真实行**（r1 `Safety & emergency`）：该帧 fusion 未合成其 menu_item（同 run 后帧
   seq16/17/19 有 menu_item）→ Pattern-5 无 peer、`LooksLikePreferenceRow` 未命中、完整性 bypass
   的 `knownStableKeys`/`IsKnownPartialRepeat` 均无证据 → 数入 Unknown。garbled `LOu` 同属此类
   （真实行 + OCR 坏块 + 主通道无判决）。

## 3. 人操作手机的思维（reality check）

人在屏幕上看到的 Settings root / Display 页：**每一行是一个条目**——`Display`、`Brightness`、
`Appearance`、`Display size and text`、`Color`、`Safety & emergency` 全是可点菜单行；`Dark theme`
是开关；`Not set` 是子文本；行之间**没有任何"未知交互元素"**；无文字矩形只是行背景；`LOu` 只是
某一行标题的 OCR 残块（真机上看就是 'Network & internet' 之类完整文字）。
→ 系统报告的 Unknown 义务在人类模型里**全部不存在**；这是"幻影义务"（phantom obligation）+ 一个
"漏判的真实行"（rogenuine gap），不是页面级未知。

## 4. 代码定位

### 4.1 语义层（`src/UniClaw.Semantic.Settings/SettingsSemanticCapability.cs`）——text-centric 盲区
- 每个 primary occurrence 的顺序 pattern（root / search / parent-return / **Pattern-5
  `IsDuplicatePrimaryRowRendering`（L316-339：需 1 个同文本+重叠 bounds 的 menu_item peer）** /
  LocalControl / `LooksLikePreferenceRow`（L309-314：**要求非空 RawText**）/ Pattern-7 副标题
  （L174-175：无 textFact 直接 continue））；
- **无文本 occurrence 结构性得不到任何 verdict** → analyzer（`InteractionAffordanceAnalyzer.Reduce`）
  无 admitted evidence → 返回 Unknown（授权=true）→ 完整性计数。
- Pattern-5 对同文本副本的抑制依 fact 层 provider-type/bounds/合成父容器不同而逐行失效
  （row_012 tb 被 generic 合成判 NavigationCandidate；row_011 tb 落空→Unknown），产生同帧双源或 Unknown。

### 4.2 fusion 层（组合行输入）
- 行背景/容器无文本元素合成（`NonInteractive` type, 无 row_id）；
- `Safety & emergency` 帧间 menu_item 合成缺失（13/14 无、16/17/19 有）——滚动子页组合行稳定性
  类问题（与 C4 veto / relation-head 家族同源，已登记）。

### 4.3 完整性 gate（`src/UniClaw.Runtime/Agent/Agent.OpenWorld.cs` L1241-1330）
- `knownStableKeys`（本观测内已知分类元素的 StableKey）+ `IsPhysicalRowDuplicate`（L1718-1745：
  恰 1 个同 key 已知元素 + 垂直重叠 ≥ 短高一半）+ `IsKnownPartialRepeat`（L1696-1707：先前
  accepted 帧出现过同 key menu_item）；
- **无文字碎片（无 StableKey）/ 无 prior 证据的漏判行（`Safety & emergency`）结构性无法 bypass**，
  fail-closed 计数 → `Unknown interaction affordances remain`。这是**按设计的 fail-closed**，
  但其上游（语义判决盲区 + fusion 无文本合成）是缺口所在。

## 5. Owner / GapKind / 下一步 gate 候选

- **Owner**：Perception/Fusion（无文本容器碎片合成、`Safety & emergency` 类 menu_item 组合缺失、
  garbled OCR）⊕ Semantic patterns（text-centric 判决盲区：无文本 occurrence 无默认 suppressive
  verdict）——**不在 normalizer 权限内**（本分析零代码修改）。
- **GapKind**：`UNKNOWN_AFFORDANCE_PHANTOM_FRAGMENT`（无文本容器碎片幻影义务）+ `SEMANTIC_VERDICT_GAP`
  （无文本/未组合行的判决盲区）。已登记同类：dual-representation、`SEMANTIC_PATTERN_PREDICATE_FACT_FRAGMENTATION`、
  ICON_TEXTLESS、Unknown 残余清单。
- **候选 gate（未授权，供 Leader 裁决）**：
  1. **语义 suppressive 默认**：无文本且无任何交互形状证据（structured 侧无 clickable/checkable/
     switch；无 toggle 形状）的 primary occurrence → NonInteractive（与 analyzer 对 structured 行
     的 Fallback 语义对齐）——结构性关闭幻影义务类；
  2. **fusion 组合行稳定性**：`Safety & emergency` 类行在滚动帧的 menu_item 组合补齐；
  3. **碎片双源**（row_012 等 tb 被 Admission）继续由 fragmentation gate 处理。
- **Phase 2.6**：维持 STOPPED；normalizer gate 目标（representation reorder 不再 Unresolved）已达成，
  下一 blocker 移交上述 Owner / Human Gate。

## 6. 边界声明

零代码修改；未放宽完整性 fail-closed；未改 semantic patterns / fusion / normalizer；
所有结论挂 fresh real stage 帧（r1/r2）+ pre-repair run-1 对照 + 源码行号。