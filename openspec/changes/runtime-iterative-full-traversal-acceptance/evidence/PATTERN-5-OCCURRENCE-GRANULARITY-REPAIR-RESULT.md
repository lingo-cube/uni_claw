# PROJECT_LEADER_PATTERN_5_OCCURRENCE_GRANULARITY_REPAIR_RESULT

> Gate: `PROJECT_LEADER_PATTERN_5_OCCURRENCE_GRANULARITY_REPAIR_GATE`
> Date: 2026-08-30
> HEAD: `1cbe7e7`（工作树；含前序 gate 未提交改动 + 并行未提交工作）
> Decision: **诊断 ACCEPTED（FACT_FRAGMENTATION 主 / REPRESENTATION_ROLE_AMBIGUITY 次）· FDP ACCEPTED ·
> 最小修复 APPROVED · 新 composition authority NOT REQUIRED · Pattern-5 语义放宽 / completeness /
> Fusion / OCR / ICON / Safety NOT AUTHORIZED · Phase 2.6 维持 STOPPED**
> 前置: `SEMANTIC-FRAGMENT-VERDICT-CONSISTENCY-DIAGNOSTIC-RESULT.md`（Pattern-5 死于 fact 粒度断裂，
> 重放器逐行证明 peers==0）。

## 0. 一句话

只修 `IsDuplicatePrimaryRowRendering` 的**输入聚合粒度**：peer 判定从 fact-level 改为
**occurrence-level**（复用 OccurrenceId 分组，聚合 RawText / Bounds / Providers），
duplicate 语义条件逐字不变。生产粒度（Text/ClassName/Geometry 三 fact 拆分）下，
同行 text_block 首次真正命中 duplicate suppression → NonInteractive，
不再退化为 Unknown / 独立 NavigationCandidate。

## 1. Minimal Diff（1 生产文件 + 1 测试文件）

| 文件 | 内容 |
|---|---|
| `src/UniClaw.Semantic.Settings/SettingsSemanticCapability.cs` | +`OccurrenceSemanticView`（capability 内部 projection，非 Runtime authority 对象）+ `ViewOf`（按 OccurrenceId 聚合 rawText/bounds/providers）+ `IsDuplicatePrimaryRowRendering` 重写为 occurrence-level（语义条件不变） |
| `tests/UniClaw.Runtime.Tests/Perception/ExternalSettingsSemanticCapabilityTests.cs` | +`FragmentedOccurrence`（projector 同构三 fact 构造）+ 11 新测试（A buyer + B–J counterexample + switch/导航行回归）；旧 mega-fact 测试保留为 compatibility case |

零改动：`SemanticObservationFactProjector` fact contract、semantic fact schema、Fusion、
`SourceGroundingNormalizer`、`InteractionAffordanceAnalyzer`、completeness、Pattern-7、OCR、
ICON、Safety。

## 2. Occurrence Aggregation Definition

```
OccurrenceSemanticView:
  occurrenceId
  rawText?    ← occurrence 组内第一个非空 RawText（Text fact）
  bounds?     ← occurrence 组内第一个非空 Geometry bounds（Geometry fact）
  providers[] ← occurrence 组内所有非空 RawProviderType（Text fact）
```
能力内部 predicate projection，仅存在于 `SettingsSemanticCapability`；不升级为 Runtime authority。

## 3. Exact Predicate Before / After（语义条件不变）

| 轴 | BEFORE（fact-level） | AFTER（occurrence-level） |
|---|---|---|
| peer 候选单元 | 单个 fact 需同时满足 text+bounds+provider | **occurrence 组**（OccurrenceId 聚合后分别取三属性）|
| 同文本 | `f.RawText == text`（单 fact） | `peer.RawText == current.RawText` |
| bounds 重叠 | `f.Bounds is {} && Overlaps(...)` | `peer.Bounds is {} && Overlaps(peer.Bounds, current.Bounds)`（既有 Overlaps 谓词）|
| provider | 单 fact provider == menu_item | `peer.Providers` 含 menu_item（OrdinalIgnoreCase）|
| 排除自身 | `!Equals(f.OccurrenceId, current)` | `!Equals(peer.OccurrenceId, current)` |
| 计数 | ==1 命中；0 不命中；>1 不命中 | 同（唯一 peer → suppression；多 peer → ambiguous fail-closed）|
| 自身为 menu_item | 提前 false | 同 |

## 4. RED→GREEN

- **RED**（未修复）：`Production_granularity_duplicate_text_block_is_noninteractive_with_unique_menu_item_peer`
  FAIL —— 生产粒度（拆分 fact）下当前 Pattern-5 peers==0，同行 text_block 无 NonInteractive
  （FACT_FRAGMENTATION 的精确复现）；其余 33 tests（B–J counterexample + legacy mega-fact +
  既有回归）全 PASS（counterexample 为"不得命中"型，本就通过）。
- **GREEN**（修复后）：**34/34**。A 命中 duplicate suppression；B–J 全部保持不命中；
  legacy mega-fact 兼容用例继续通过。
- **实帧预览（重放器，真实 r4 seq10 + 修复后 capability）**：10 个同行 text_block
  （Brightness/012/013/014/015/017/019/020/025/026）全部 → NonInteractive（含此前经 XML
  corroboration 被提升为 NavigationCandidate 的 4 个 —— Pattern-5 现在先于 corroboration 命中）；
  `'83%'`（Pattern-7）→ NonInteractive；无 peer 的 `Not set`/`Will never` 不变（residual）。

## 5. Counterexample Matrix（A–J）

| case | 输入要点 | 结果 |
|---|---|---|
| A exact production-granularity duplicate | 同文本 + overlap 为真 + 唯一 menu_item peer，facts 全拆分 | **命中 NonInteractive** |
| B same text, no overlap | bounds 不重叠 | 不命中 |
| C overlap, different text | 文本不同 | 不命中 |
| D peer provider ≠ menu_item | peer 为 text_block | 不命中（text_block peer 永不 suppress）|
| E 两个 menu_item peers | 歧义 | fail-closed 不命中 |
| F missing geometry | current 无 Geometry fact | 不命中 |
| G missing text | rawText 空 | 不命中 |
| H 自身多个 facts 不作 peer | 单 occurrence | 不命中（不自杀）|
| I row_id 不作为 duplicate proof | 谓词不读 row_id（fact 层无此输入）—— same-text/overlap 之外无捷径 | 保持（B/C 机制覆盖）|
| J XML-only corroboration 不作证明 | 仅 auxiliary 行无 primary menu_item peer | 不命中（XML 不产生 duplicate；允许既有 corroboration Nav）|

回归：genuine navigation row（menu_item）→ Nav；interactive switch/toggle（无匹配）→ LocalControl；
Pattern-7 副标题、Search/ChildOf、Fusion publication repair、source normalizer repair 均未触碰。

## 6. Deterministic Regression

- 语义目标套件 `ExternalSettingsSemanticCapabilityTests`：**34/34（RED→GREEN 全绿）**。
- 全量 C# 确定性套件（`dotnet test src/UniClaw.Runtime.sln`）：**2344 passed / 5 failed（2349）**——
  5 个失败全为环境性既有（`CORR_HOST03/04/09` vision identity 漂移、`Capstone_OneAgentOneRun_RealEmulator`、
  `ExternalBoundary_RealDevice`），与前一 gate 同集合同签名，**零新增**（pre-existing 与新增分开）。
- `git diff --check`：CLEAN。

## 7. Fresh Real Buyer（before/after，r4 → r5）

> 环境：emulator-5554 + pubfix shadow receipt（本 gate 零感知改动 → pipelineRevision 不变，复用）。

### BEFORE（r4，修复前，Display child accepted 帧）

| 指标（seq10 为代表） | r4 值 |
|---|---|
| eligible Unknown count | 8（含 6 个有同行 menu_item peer 的 tb：Brightness/Lock display/Appearance/Color/Color contrast/Other display controls）|
| duplicate text_block count（P5 命中）| **0**（P5 死）|
| text_block → NavigationCandidate（XML corroboration 提升）| 4（Brightness level/Lock screen/Screen timeout/Display size and text）|
| text_block → NonInteractive（duplicate）| 0 |
| menu_item NavigationCandidate | 13 |

### AFTER（r5，修复后，accepted child 帧 seq25）

| 指标（seq25） | r5 值 |
|---|---|
| eligible Unknown count | **2** —— 仅 `Not set`(row_025) / `Will never`(row_027)，**均为无同行 menu_item peer 行（§7 residual，按要求保留）** |
| duplicate text_block count（P5 命中）| **11**（含 Brightness/Brightness level/Lock display/Lock screen/Screen timeout/Appearance/Display size and text/Color/Color contrast/Other display controls/83% 类）|
| text_block → NavigationCandidate | **0**（原 corroboration 提升的 4 行被 duplicate 先消解）|
| text_block → NonInteractive（duplicate）| **11** |
| menu_item NavigationCandidate | menu_item 行正常（child 仍推进到 seq25）|
| root | `open-world container inventory complete: sources=17, unresolved=0` ✅ |

### 判定

- 目标 Reality 改善证实：同行 text_block 的 secondary 义务（Unknown / 独立 Nav）**显著消失**，
  转为 NonInteractive（duplicate）；无 peer 的真实残余（Not set/Will never）保持不变；completeness
  fail-closed 零改动（语义层在完整性之前兑现 verdict）。
- 本 run terminal：`quiescence admission budget exhausted (last seq=28, attempts=2,
  classification=left container)` —— **下一 blocker 前移**（viewport-acceptance/quiescence 类，
  child 第二窗 seq28 未被确认；与既有 quiescence 类 blocker 同属，非本 gate / 非 Unknown）。

## 8. Debug IR / EvidenceRef / AssetRef Buyer Sample（dogfood）

```
CASE: Pattern-5 Fact Granularity Mismatch
Run/ObservationSeq: settingscampaign r4 · seq10（accepted Display-child 帧）
TraceRef:  /tmp/p26-normalizer-pubfix-r4-stage.json（semanticAdmission：idx2/5/9/13/14/15 零 envelope；
           idx3/6/7/12 为 NavigationCandidate；idx4 '83%' NonInteractive）
EvidenceRef: /tmp/p26-semantic-replay（OccurrenceMatrix：14 个 text_block P5 peers 全 0；
             peer occurrence 解码 chan=vision[16..27]，Provider=menu_item 且组内 Geometry 存在）
AssetRef:  r4 seq10 fusedCandidates idx2（tb 'Brightness',row_011）+ idx16（mi 'Brightness',row_011,
           同 bounds）对 —— 关联层：StageEvidence / ObservationSeq 10 / occurrence
           vision:2 vs vision:16 / 帧截图未持久化（本 run 未保存 image crop；以 stage fusedCandidates
           bounds 为证据坐标）
ReplayRef: /tmp/p26-semantic-replay（修复后：上述 10 个 tb → NonInteractive）
Exact falsifier: Production_granularity_duplicate_text_block_is_noninteractive_with_unique_menu_item_peer
FDP: 谓词要求单 fact 共位 text+bounds+provider，而 projector 拆分三 fact → peers==0（A）→ 修复聚合粒度
Owner: SettingsSemanticCapability predicate input aggregation
```

## 9. Residual First Blocker / Phase 2.6

- **Residual（不修，记录）**：`Not set` / `Will never`（无同行 menu_item peer）继续按现有
  Pattern-7 / Unknown 规则处理（r5 中恰好为仅剩的 2 个 eligible Unknown —— 真残余，非 phantom）；
  ICON_TEXTLESS / `Safety & emergency` / `LOu` / OCR 若成为下一 first blocker 只登记。
- **下一 first blocker（记录，本 gate 不修）**：r5 child 第二窗 seq28 未被 quiescence
  admission 确认（`classification=left container`）→ `viewport exploration did not prove
  positive exhaustion`。Owner = viewport-acceptance / page-continuity 层（与既有 quiescence 类
  blocker 同族），非本 gate。
- **Phase 2.6 维持 STOPPED**；本 gate 完成后停止，等待 Human。

## 10. AuthorityDelta / ArchitectureDelta

- **AuthorityDelta: NONE**（capability 内部谓词输入粒度修复；无授权/所有权变化）。
- **ArchitectureDelta: NONE**（无新 abstraction/boundary/lifecycle；`OccurrenceSemanticView` 为
  capability 内部 projection，不升级；duplicate 语义条件零放宽）。
- 冻结不变量保持：`FACT != OCCURRENCE`；`PREDICATE_REQUIRING_OCCURRENCE_PROPERTIES` 不再假设
  单 fact 共位；`SECONDARY_REPRESENTATION != INDEPENDENT_INTERACTION_OBLIGATION` 首次被语义层兑现。

## 11. Boundary Declaration

零触碰：completeness（Unknown fail-closed / IsPhysicalRowDuplicate / knownStableKeys / prior-repeat
bypass）、`InteractionAffordanceAnalyzer`、`SourceGroundingNormalizer`、Fusion（publication repair 未动）、
projector fact contract、Pattern-7、OCR、ICON、Safety；未按 row_id/StableKey/text/bounds/XML 单项证明
duplicate。