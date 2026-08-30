# POST_COMPLETENESS_NAV14_TIER_SCOPE_DIAGNOSTIC_RESULT

> 前置：`ROW-BAND-SUB-ELEMENT-BOUNDED-REPAIR-RESULT.md`（child discovery epoch 首次完整 sources=17）后，
> runF terminal 前移至 post-completeness 一致性。本诊断为 **Option A 有界收尾的最后一环：'nav:14' 归类**。

## 0. 一句话

`Post-completeness fresh occurrence 'nav:14' does not resolve…` 的 'nav:14' =
**AuxiliaryStructured（XML）导航候选 'Dark theme'**。FDP：`PostCompletenessConsistencyValidator.Validate`
（B.3）遍历 **全部** `OccurrencesOf(fresh)`（不含 eligible 过滤），把 auxiliary 候选当 fresh 义务去匹配
**只含视觉签名**的冻结类——而 structured 签名（`RawText|Class|ResourceId|CD`）与视觉签名
（`StableKey|Type`）格式互不相容、**结构性永远不在冻结类里** → 新鲜帧只要携带 XML 导航行就必然失效。
这是层级原则下的又一"被修复揭开的下一层 fail-closed"，且为**可证伪阳性**（tier 错配，非真实新源）。

## 1. 证据（runF，seq31 = 验证时的容器当前观测）

- 冻结类（child epoch，Normalize([22,25,28,31])）= 17 个**视觉**签名（`row_010..row_036|menu_item/text_block`）。
- seq31 `OccurrencesOf` 序：13 个 PrimaryVision(eligible) nav（nav:1..13）→ **4 个 AuxiliaryStructured(ineligible) nav（nav:14..17）**：
  | nav | 渠道 | text | elig |
  |---|---|---|---|
  | 14 | AuxiliaryStructured | 'Dark theme' | False |
  | 15 | AuxiliaryStructured | 'Display size and text' | False |
  | 16 | AuxiliaryStructured | 'Colors' | False |
  | 17 | AuxiliaryStructured | 'Color contrast' | False |
- 'nav:14'（'Dark theme' XML 行，clickable LinearLayout）签名 ∉ 冻结集 → `NoClass` → invalidated。
- **结构性必然**：`BuildFrozenSources` 只收录 `UniqueSourceSignatures`（normalizer 输出仅含 eligible/视觉
  签名）；auxiliary 候选"永远不能 grounded DFS"（设计不变量）→ 它们在任何新鲜帧中都不可能解析 → 校验路径
  对含 XML 行的新鲜帧**必失效**。

## 2. 分类 / FDP / Owner

- FDP：`PostCompletenessConsistencyValidator.Validate` B.3 的 fresh 义务集**未按授权层过滤**（遍历全部
  OccurrencesOf），与该层自身冻结集的授权层范围不一致——tier 错配导致必然误失效。
- Owner：`PostCompletenessConsistencyValidator`（World/completeness seam）。
- 非新源、非真实残余：'Dark theme' 等 XML 行与视觉行同一物理行，属 corroboration tier。
- 既有 frozen 不变量保持：`SECONDARY_REPRESENTATION != INDEPENDENT_INTERACTION_OBLIGATION`；
  auxiliary ≠ logical source（`SourceGroundingValidator` 拒绝 auxiliary-only grounding）。

## 3. Minimal Repair Candidate（NOT AUTHORIZED，供裁决）

在 `PostCompletenessConsistencyValidator.Validate` B.3：**仅对 `EligibleForAuthorization` 的 occurrence
做 frozen-class 解析**（跳过 auxiliary），与该层冻结类的授权层范围一致：
- eligible（视觉）fresh 候选**全部仍必须唯一解析** → fail-closed 语义零放宽（真实新源仍会失效）；
- auxiliary corroboration 不再被当作不可能满足的义务 → 消除结构性误失效；
- 一致性：冻结期与校验期都以授权层为义务面，与 "auxiliary 不产生 logical source" 不变式对齐。
不触碰：`SourceGroundingNormalizer`、normalizer、Fusion、P5/P7、quiescence、Unknown bypass。

## 4. 阶段性裁决点（Leader）

诊断完成——这是当前链条的可判定位置：
- **A1 继续**：授权上述最小修复（1 个小 gate：语义范围对齐 + RED→GREEN + 全量 + fresh real）；
  预期 child epoch 完整 + post-completeness 通过 → 接近 terminal=Completed。
- **A2 宣布阶段性结果**：现状已可如实表述 = "child epoch 完整证明已达成（sources=17）；其余为已编目、
  可证伪误失效（tier 错配）与已登记残余；启动器/环境偶发事件另计"。证据已足够厚，停止继续烧轮次。

## 5. Phase 2.6 / 边界

- **Phase 2.6 维持 STOPPED**（裁决前）。
- 零代码修改；未触碰 completeness fail-closed 本身（候选修复仅对齐义务层范围，不放松任何 eligible 检查）。