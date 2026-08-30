# PROJECT_LEADER_SOURCE_NORMALIZER_ORDER_SENSITIVITY_DIAGNOSTIC_RESULT

> Gate: `PROJECT_LEADER_SOURCE_NORMALIZER_ORDER_SENSITIVITY_DIAGNOSTIC_GATE`
> Date: 2026-08-30
> HEAD: `1cbe7e7`（工作树；含上一 gate 未提交的 bounds-repair 改动）
> Decision: **语义投影 bounds 修复 ACCEPTED · Phase 2.6 维持 STOPPED · 顺序敏感诊断 APPROVED · 生产修复 NOT AUTHORIZED**
> 方式: **DIAGNOSIS ONLY（零代码修改）**。目标 run: `/tmp/p26-projection-repair-r1-*`
> （post-bounds-fix fresh real run-1，21 frames；Display child accepted `[22, 25, 28, 31]`，
> terminal = `Source normalization is unresolved; completeness cannot be proven.`）
> 结论: **A. REPRESENTATION_ORDER_DRIFT** —— 真实 top-to-bottom 逻辑序未变，
> 变的是 detector/fusion/canonical element array 的序列化顺序。

## 0. 一句话

**先确认"页面真的换顺序了"还是"相机把同一页面换了个数组顺序"→ 答案是后者**：
seq22 与 seq25 是同一 Display 子页、同一排版（12 个共享行空间秩次完全一致、CenterY 恒平移
Δ≈−0.146 ≈ 350px，仅视口滚动差）；变的是感知序列化——`row_010`（Display 工具栏标题带）在
seq22 中以 **partial-width（X1=0.0667…, X2=0.3403）** 被发射到 raw element idx13 / canonical pos3，
在 seq25 中以 **full-width（X1=0, X2=1）** 被发射到 idx0 / canonical pos0，打乱 signature 数组顺序，
使 `TryAnchorBasedMerge` 的 anchor 单调性谓词 fail-closed。

## 1. 结论速览

| 项 | 结论 |
|---|---|
| 分类 | **A. REPRESENTATION_ORDER_DRIFT**（B 不成立：逻辑序未变；C 不成立：SameSource 可靠成立） |
| 失败 pair | pair 1（seq22→seq25），非此前假设的 pair 3（28→31） |
| 触发谓词 | `TryAnchorBasedMerge` anchor 单调性检查：union idx 序列 `[3,0,1,2,4,5,6,9,10,11,12,13,14]` 非单调 → fail-closed（严格重叠与 boundary 放宽两级在它之前已先行失败） |
| FDP | `SourceEquivalenceNormalizer` 的顺序敏感谓词操作数是 **element-array（fusion 发射）顺序**，而该顺序 ≠ 逻辑/空间顺序；`row_010` 工具栏在两帧间的数组位置翻转（pos 3→0） |
| Owner | `SourceEquivalenceNormalizer`（`src/UniClaw.Runtime/World/`，Runtime/World normalization） |
| GapKind | `NORMALIZER_ORDER_SENSITIVITY` / `REPRESENTATION_ORDER_DRIFT`（数组序漂移，非真实 reorder、非 identity 变化） |
| 上游触发（不修） | 感知 serialization：partial→full-width 检测差异 + fusion 发射序变化 + `row_010` 文本带在 seq25 降级为 NonInteractive 子元素（已登记 `SEMANTIC_PATTERN_PREDICATE_FACT_FRAGMENTATION` 同源形态） |
| 最小修复候选 | 逻辑序（spatial）投影 + 同 StableKey 行分组邻接 —— ORDER-ONLY 修复，identity/计数/guard 零放宽（§8，未实施） |
| Phase 2.6 | 维持 **STOPPED**；下一 gate 提交 `SOURCE_NORMALIZER_LOGICAL_ORDER_RECONCILIATION_REPAIR_GATE` |

## 2. seq22 / seq25 同容器 accepted 帧 reality comparison

证据源（全部来自 post-bounds-fix run-1，未重新采集、未用未来帧）：

| Artifact | 内容 |
|---|---|
| `/tmp/p26-projection-repair-r1-frames.json` | 21 帧 canonical elements（idx/row_id/text/type/bounds）+ structured |
| `/tmp/p26-projection-repair-r1-stage.json` | `acceptedViewportDecisions` + `runtimeTrace`（46 条） |
| `/tmp/p26-windows2.json` | normalizer 确定性重放输入（= `[22(15), 25(17), 28(17), 31(17)]`，与 repair-result §8 一致；逐签名对照 frames.json raw elements 全部在场） |

### 2.1 采纳链（stage evidence，`acceptedViewportDecisions` + trace）

- root：CONFIRMED 5/8/11/14/17/19；`open-world container inventory complete: sources=16, unresolved=0, seq=[2,5,8,11,14,17,19]`。
- Display child：`viewport exploration continue: source-seq=22`（**首个 accepted observation**，非 scroll-CONFIRMED 帧）→ `scroll stability CONFIRMED (seq=25/28/31, attempt 2)` → `viewport exploration exhausted: source-seq=31`。
- terminal：`Source normalization is unresolved; completeness cannot be proven.`（repair-result §8 已登记）。
- **视口事实**：seq22 与 seq25 之间页面滚动 ≈ −0.146（≈350px @2400 高；§2.3 恒平移证明）。
  接受对（首窗 22 与首确认帧 25）跨越一次滚动位移——acceptance 层在视口仍移动时接纳了首窗
  （设计上下文，非本 gate 修复对象）。

### 2.2 raw element order vs canonical signature order（同一帧内的两级顺序）

**seq22**（33 elements / 6 structured）：

| raw idx | row_id | type | text | bounds (X1..X2 | Y1..Y2) | 空间 rank（本帧内） |
|---|---|---|---|---|---|---|
| 0 | – | icon | | 0.0486..0.0889 | 0.0788..0.0975 | （无 row_id，不入序列） |
| 1 | row_010 | text_block | Display | **0.0667..0.3403** | 0.1875..0.2269 | 1（工具栏带，partial-width） |
| 2..12 | row_019..row_028 | text_block | … | … | … | 2..12 |
| **13** | **row_010** | **menu_item** | **Display** | **0.0667..0.3403** | **0.1875..0.2269** | **1（同带）** |
| 14..32 | row_019..row_032 | menu_item / NI | … | … | … | 2..13 |

**seq25**（42 elements / 7 structured）：

| raw idx | row_id | type | text | bounds (X1..X2 | Y1..Y2) | 空间 rank |
|---|---|---|---|---|---|---|
| **0** | **row_010** | **menu_item** | **Display** | **0..1（full-width）** | **0.0625..0.1175** | **1（工具栏带）** |
| 1 | – | icon | | 0.0472..0.0903 | 0.0788..0.0975 | （无 row_id） |
| 16 | row_019 | menu_item | Brightness | 0.0597..0.2222 | 0.1369..0.1531 | 2 |
| 17..41 | … | menu_item / NI / tb | … | … | … | 2..14 |
| 18 | row_010 | **NonInteractive** | Display | 0.1764..0.3458 | 0.0719..0.1038 | （工具栏文本带降级为子元素） |

- raw element **idx** 只是感知序列化序：seq22 中 row_010 的菜单 occurrence 被发射到 idx13
  （空间 rank 1 却在数组中位列第 14），seq25 中发射到 idx0（恰与空间 rank 1 重合）。
- canonical signature 数组（= normalizer 实际输入，windows2.json）：

```
C22 (seq22, 15 sigs): [row_020|text_block, row_022|text_block, row_023|text_block, row_010|menu_item,
  row_019|menu_item, row_020|menu_item, row_021|menu_item, row_030|menu_item, row_022|NonInteractive,
  row_023|menu_item, row_025|menu_item, row_031|menu_item, row_027|menu_item, row_028|menu_item, row_032|menu_item]
C25 (seq25, 17 sigs): [row_010|menu_item, row_020|text_block, row_022|text_block, row_023|text_block,
  row_027|text_block, row_019|menu_item, row_020|menu_item, row_021|menu_item, row_022|menu_item,
  row_023|menu_item, row_025|menu_item, row_031|menu_item, row_027|menu_item, row_028|menu_item,
  row_032|menu_item, row_035|menu_item, row_034|menu_item]
```

⇒ `row_010|menu_item` 双帧签名完全一致（同 StableKey + 同 PerceptionType）；**数组位置 3→0 翻转**。

### 2.3 bounds top-to-bottom spatial order（逻辑序 truth）

12 个共享行 CenterY（frames.json 原始 bounds 计算，任一 type 取最小带）：

| row_id | C22 CenterY | C25 CenterY | Δ |
|---|---|---|---|
| row_010 | 0.2072 | 0.0900 | −0.1172 * |
| row_019 | 0.2909 | 0.1450 | −0.1459 |
| row_020 | 0.3444 | 0.1972 | −0.1472 |
| row_021 | 0.4334 | 0.2875 | −0.1459 |
| row_022 | 0.4838 | 0.3378 | −0.1459 |
| row_023 | 0.5700 | 0.4234 | −0.1466 |
| row_025 | 0.6613 | 0.5153 | −0.1459 |
| row_027 | 0.7997 | 0.6538 | −0.1459 |
| row_028 | 0.8662 | 0.7203 | −0.1459 |
| row_031 | 0.7119 | 0.5656 | −0.1462 |
| row_032 | 0.9184 | 0.7722 | −0.1462 |
| row_024 | 0.5950 | 0.4491 | −0.1459 |

`*` row_010 的 Δ 偏差（−0.117 vs −0.146）来自 seq22 的 partial-width 文本带检测位移（工具栏带在 seq22
内部也不自洽：ic on 在 0.0788、文本带在 0.207）；**秩次不受影响**。

**秩次比较（共享行，top-to-bottom）**：两帧完全一致 ——
`row_010 < row_019 < row_020 < row_021 < row_022 < row_030 < row_023 < row_025 < row_031 < row_027 < row_028 < row_032`。
seq25 仅多出底部两行 `row_035`(Color contrast)、`row_034`(Other display controls)（滚动所致），
seq22 底部无；**没有任何一行跨越另一行**。⇒ 逻辑序 = 未变。

### 2.4 admitted NavigationCandidate 顺序 / anchor selection / union index / monotonicity 谓词

`Normalize` 三级重放（`SourceEquivalenceNormalizer.cs` 逐行复刻；exact Ordinal）：

| operator/predicate | result | evidence |
|---|---|---|
| 帧内非空 / 无重复签名 | pass | 15 / 17 / 17 / 17，无 in-frame exact duplicate |
| strict suffix(union)-prefix(window) | noop | union tail `…row_028, row_032` vs window head `row_010, row_020 text_block…` 无连续重叠 |
| boundary skip-first / last / both | noop | 三者均无 unique overlap |
| anchors（first-match，deterministic） | 13 anchors | window→union idx = `[3, 0, 1, 2, 4, 5, 6, 9, 10, 11, 12, 13, 14]` |
| **anchor 单调性**（`orderedByWindow[i].UnionIdx ≤ [i-1].UnionIdx → null`） | **failed** | `3 → 0` 即反转 → `TryAnchorBasedMerge` 返回 null |
| final | **Unresolved（fail-closed）** | pair 1 即失败；28→31 形状未到达 |

**第一个反转 `3 → 0` 的精确来源**：window row 0 = `row_010\|menu_item` → union idx 3
（seq22 中 row_010 在数组序第 3 位）；window row 1 = `row_020\|text_block` → union idx 0。
即 `row_010`（工具栏）在 seq22 的**数组序**排在 `row_020` 之后（虽然空间上它在上方），在 seq25
排在 `row_020` 之前——**数组序对这两个 row 在 seq22 是空间倒置的，在 seq25 恰好空间正确**。
锚点集合本身无歧义（13 个锚、first-match 唯一），失败纯粹来自顺序谓词的操作数。

## 3. Exact First Divergence

> **系统的世界信念（logical source 顺序）没有偏差；偏差发生在"作为 Normalize 输入的顺序对象"上。**
> Normalize 消费的 canonical signature 数组顺序跟随 fusion 发射序（element-array 序），而发射序
> 是感知 serialization 的产物：seq22 把空间第一行（row_010 工具栏）发射到数组第 3/13 位，seq25
> 发射到第 0 位。`TryAnchorBasedMerge` 的单调性谓词把这个数组序当作逻辑序来验证，于是对一个
> **逻辑上完全前向**的窗口判定为"向后/矛盾"而 fail-closed。

- 第一发散点（FDP）：**`TryAnchorBasedMerge` 单调性检查的操作数 = 非空间顺序的 signature 数组**
  （union 侧由 seq22 的 fusion 发射序决定，window 侧由 seq25 的发射序决定），非单调源于
  `row_010` 数组位置翻转 —— 不是"页面真实 reorder"，不是"锚点选错"，不是"签名不匹配"。
- 上游触发链（均为表示噪声，非本 gate Owner）：seq22 partial-width 工具栏带（bounds 修复前
  该形态甚至会 DROP 整帧，现合法存活）→ fusion 发射序随之改变；`row_022|text_block` /
  `row_022|NonInteractive` / `row_027|text_block` 等 fragment 进序列（已登记
  `SEMANTIC_PATTERN_PREDICATE_FACT_FRAGMENTATION` 同源形态）。

## 4. A / B / C Classification（证明）

| 假设 | 判定 | 证据 |
|---|---|---|
| **A. REPRESENTATION_ORDER_DRIFT** | ✅ **成立** | 逻辑/空间秩次双帧全等（§2.3）；共享行 CenterY 恒平移 −0.146（纯滚动，非排版变化）；唯一漂移是 raw/canonical 数组位置（§2.2） |
| B. TRUE_LOGICAL_ORDER_CHANGE | ❌ 不成立 | 若真实顺序改变，空间秩次或 CenterY 相对关系必然变化；实测 12 行秩次与相对间距全等；`row_035/034` 仅是滚动新入视口，非 reorder |
| C. IDENTITY / COMPOSITION_CHANGE | ❌ 不成立 | ① identity 契约 = `StableKey \|\| Text \| PerceptionType`，bounds 明确"never identity"（`BuildSignature` 注释）；② `row_010\|menu_item` 双帧字符串全等；③ 稳定器按文本+位置 band 指派 row_010，两帧均为子页**顶行**（空间 rank 1）；④ partial→full-width 是同一 source 的 composition 表示变化（seq25 中文本带降级为独立 NonInteractive 子元素），不是新 source |

## 5. 五问直答

1. **element index 是否只是 perception serialization order？** 是。seq22：row_010 空间 rank 1 却占 raw
   idx13 / canonical pos3；同一帧内 raw 数组序既非 Y 序也非 canonical 序；seq25 中同一元素在 idx0。
   idx 是 detector/fusion 在本帧流水线中的发射序号，不承载逻辑顺序语义。
2. **top-to-bottom logical UI order 是否实际保持不变？** 是（§2.3）：12 个共享行秩次全等、ΔY 恒平移
   ≈−0.146（≈350px 滚动），无跨行；seq25 底部新增 row_033/034/035 为滚动入视口。同一页面同一排版。
3. **`row_010` Display 是否被可靠证明为 SameSource？** 是：同 StableKey（row_010）+ 同文本
   （Display）+ 同 PerceptionType（menu_item）+ 双帧空间 rank 均为顶行（滚动平移一致），四重证据；
   identity 契约本身排除 bounds，故 partial→full-width 不改变身份。
4. **monotonicity invariant 真正想保护的是 raw element array order 还是 logical/spatial source order？**
   意图是 **logical/spatial source order**（代码注释 "backward scroll reverses union order"、
   REVISIT_COMPLETENESS_FRESHNESS_PRESSURE：非单调/纯重复 revisit 不得 resolve），但**操作数**是
   signature-array 顺序（fusion 发射序）——两者仅在"感知发射序 == 空间序"的隐假设下重合。
   本 case 暴露该隐假设不成立：逻辑前向的窗口被误判为矛盾（false negative），而 guard 真正的两个
   防御（每窗必须新增 ≥1 source：check (a)；窗口不得与已累积的前向顺序矛盾：check (b)）在
   logical-order 操作数上依然原样成立。
5. **当前是否已有稳定 spatial/logical ordering primitive 可复用？** 有，且已在 runtime 使用：
   ① `ElementBounds.CenterY` + `Agent.OpenWorld.NavigationRowCenters` / `IsViewportStable`
   （`ScrollStabilityBoundsEpsilon` 位置漂移比较）——基于 occurrence CenterY 的空间比较先例；
   ② 感知层 row stabilizer（按文本+位置 band 指派 StableKey）——行身份的空间 banding 先例。
   Normalizer 现完全不用顺序几何（identity 排除 bounds 是正确且必须的；**ordering ≠ identity**——
   用 CenterY 做**排序键**不违反 "bounds 不作 identity"）。

## 6. Owner / GapKind

- **FDP / Owner**：`SourceEquivalenceNormalizer`（`src/UniClaw.Runtime/World/SourceEquivalenceNormalizer.cs`
  `TryAnchorBasedMerge` + `Normalize` 序列化顺序）——顺序敏感谓词的操作数与意图不一致（Runtime/World 层）。
- **GapKind**：`NORMALIZER_ORDER_SENSITIVITY` / `REPRESENTATION_ORDER_DRIFT`（A 类）。
- 不属于本 gate 修改面（声明不触碰）：单调性 guard、text 匹配、未来帧修正、completeness、
  fusion/semantic patterns、perception 层（partial/full-width 检测质量、row_022 角色翻转、
  `SEMANTIC_PATTERN_PREDICATE_FACT_FRAGMENTATION` 均为已登记上游质量问题，各自有 Owner）。

## 7. 最小修复候选（NOT AUTHORIZED，供 repair gate 评审；ORDER-ONLY 修复）

在 `ExtractNavigationSignatures`（或 `Normalize` 入口）把每个窗口的 signature 序列按
**逻辑/空间序投影**后再进入原有三级合并管道：

1. **空间序键**：每个 occurrence 的 canonical `Bounds.CenterY`（已有 primitive，非新抽象）；
2. **同 StableKey 行分组邻接**：同一 `row_id` 的变体签名（text_block / menu_item / NonInteractive
   fragment）作为**一个行组邻接排放**，组键 = 该行的主（menu_item）band CenterY；
3. **确定性 tie-break**；集合不变——不删除、不插入、不改变签名（**frame 内去重检查、source 计数、
   completeness 语义逐字不变**）；
4. **全部 guard 原样保留**：strict overlap、boundary 放宽、anchor 单调性 check (b)、
   零插入 pure-repeat 拒绝 check (a)——只是谓词的操作数变成逻辑序。

**数值验证（同输入 `[22(15), 25(17)]`，§2.2 序列 + frames.json bounds 投影）**：

| 变体 | anchor 映射 | 单调 | 结果 |
|---|---|---|---|
| 现状（数组序） | `[3,0,1,2,4,5,6,9,10,11,12,13,14]` | ❌ | Unresolved |
| 仅 CenterY 投影 | `[0,1,3,2,4,5,8,9,10,11,12,13,14]` | ❌（残余 1 处：row_020 tb/mi 双表示 band 抖动） | Unresolved |
| **CenterY 投影 + 同 StableKey 行分组邻接** | `[0,1,2,3,4,5,8,9,10,11,12,13,14]` | ✅ | **Resolved**，插入 4 个新行（`row_022\|menu_item`、`row_027\|text_block`、`row_035\|menu_item`、`row_034\|menu_item`） |

- 残余反例形态（必须在 repair gate 里钉死）：row_020 tb/mi 双表示在 seq22 中 band 中心不同
  （0.3525 vs 0.3444）而 seq25 中相同（0.1972）——即双表示几何噪声会单独破坏纯 CenterY 投影的
  单调性；行分组邻接规则正是为吸收它而设，与已登记 `SEMANTIC_PATTERN_PREDICATE_FACT_FRAGMENTATION`
  同源。falsifier 还需包括：真实 backward 帧仍 fail-closed、pure-repeat 仍 fail-closed、同帧歧义仍
  fail-closed、不同滚动偏移的相邻 accepted 窗（本 case 即 350px 位移）必须 resolve。
- **边界**：本候选不改 identity（`BuildSignature` 不动）、不改计数/completeness、不改 fusion/semantic
  模式、不删 monotonicity guard、不做全局 sort-elements-to-pass（排序只作用于**每个窗口的序列投影**，
  且由意图推导，非为个案硬编码）。

## 8. Phase 2.6 Next Gate

- **提交 `SOURCE_NORMALIZER_LOGICAL_ORDER_RECONCILIATION_REPAIR_GATE`**（A 类成立）：
  实施 §7 候选（CenterY 投影 + 同行分组邻接）+ 上述 falsifier 回归集（RED→GREEN：先证 HEAD 重放
  pair 1 Unresolved，再证修复后 Resolved 且守卫反例全绿）→ `dotnet build/test` +
  `scripts/check-consistency.sh` → 真机 settingscampaign 复验 Display child resolution
  （sources 完整、terminal 不再 `Source normalization is unresolved`）。
- **生产修复仍未授权**；本 gate 零代码修改。
- Phase 2.6 **维持 STOPPED**（Leader 指令：修复 gate 完成并复验前不自动重入）。

## 9. 边界声明

零代码修改；未删除/放宽 monotonicity guard；未按 text 强匹配（全部 exact Ordinal）；
未用未来窗口修正当前窗口；未修改 completeness / Fusion / semantic patterns / perception 层；
未为通过个案直接 sort 全部 elements。已登记上游质量问题（`SEMANTIC_PATTERN_PREDICATE_FACT_FRAGMENTATION`、
`Color contrast` StableKey 漂移、dual-representation 双表示）如实记录，不混入本 gate。

## 10. Artifacts

| Artifact | 用途 |
|---|---|
| `/tmp/p26-projection-repair-r1-frames.json` | seq22/25 raw/canonical elements + bounds（§2.2/2.3 数据源） |
| `/tmp/p26-projection-repair-r1-stage.json` | acceptance 链 + runtimeTrace（§2.1） |
| `/tmp/p26-windows2.json` | normalizer 确定性重放输入（§2.2 序列，15/17/17/17） |
| 本 gate 重放脚本（python3，seeding 于 §2.4/§7 表格） | 三级合并重放 + 三种投影变体数值验证 |