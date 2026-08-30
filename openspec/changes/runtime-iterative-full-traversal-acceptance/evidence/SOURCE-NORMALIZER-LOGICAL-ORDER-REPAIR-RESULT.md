# PROJECT_LEADER_SOURCE_NORMALIZER_LOGICAL_ORDER_RECONCILIATION_REPAIR_RESULT

> Gate: `PROJECT_LEADER_SOURCE_NORMALIZER_LOGICAL_ORDER_RECONCILIATION_REPAIR_GATE`
> Date: 2026-08-30
> HEAD: `1cbe7e7`（工作树；含上一 gate 未提交的 bounds-repair 改动 + 并行未提交工作）
> Decision: **诊断 A REPRESENTATION_ORDER_DRIFT ACCEPTED · 最小 ORDER-ONLY normalizer 修复 APPROVED · Phase 2.6 维持 STOPPED**
> 前置诊断: `SOURCE-NORMALIZER-LOGICAL-ORDER-DIAGNOSTIC-RESULT.md`（A 类证明：
> seq22↔seq25 逻辑序不变、仅感知序列化变化、row_010 SameSource 可靠成立）
> 本 gate: 实施 + RED→GREEN + counterexample preservation + permutation property +
> 全量确定性套件 + 机械检查 + fresh real campaign 复验。

## 0. 一句话

在 `SourceEquivalenceNormalizer` 内为每个 accepted 窗口构造 **deterministic logical-order projection**
（StableKey 行分组 + CenterY 行带排序），使顺序敏感谓词（suffix-prefix overlap / anchor monotonicity /
boundary plausibility）操作在 **logical/spatial order** 上而非 perception serialization order 上；
并给 boundary 放宽加 **空间边缘 plausibility 门**（防止投影后真实中页行被误判为"视口截断垃圾"丢弃）。

## 1. Minimal Diff

### 生产（1 文件，`src/UniClaw.Runtime/World/SourceEquivalenceNormalizer.cs`，+164/−14）

1. **`ExtractLogicalOrderProjection(Observation)`**（替换 `ExtractNavigationSignatures`）：
   - 相同过滤器、相同签名集合（无 dedupe / 无发明；in-window 重复签名检查与 union/completeness
     语义逐字不变）；
   - 项目定义：`key = (rowBand asc, elementIndex asc)`；`rowBand(group) = min(有效 CenterY)`，
     group = logical row key = **StableKey（primary Vision 且有行身份时；同行的 menu_item /
     text_block / NonInteractive 变体保持邻接）**，否则 = 该条目自身签名（帧内唯一）；
     无有效 bounds 的成员排最后；elementIndex 仅作确定性 tie-break，**永不参与 identity**；
     `OccurrencesOf` / 原始 Observation / canonical 顺序一概不改写（只排序 comparison projection）。
   - 附带返回 `RowBands`（ORDERING evidence only，喂给 boundary 门）。
2. **boundary plausibility 门**（`TryBoundaryRelaxation`）：skip-first/last 仅当被跳行的
   row band 位于空间视口顶/底边缘带（`BoundaryTopBand=0.1f` / `BoundaryBottomBand=0.9f`；
   无有效 bounds 保留 legacy 行为）。投影后窗口 head = 空间顶行，若该行是中页真实新行
   （如 quiescence S3/S11 的 Target band 0.25），boundary tier 不再把它当顶部截断垃圾
   静默截断出 union（completeness 数据丢失）——anchor tier 会把它插入到上方锚点之前。
3. 其余（strict overlap、anchor merge 单调性 guard、零插入 pure-repeat 拒绝、evidence 语义）**零改动**。

### 测试（1 文件，`tests/UniClaw.Runtime.Tests/Unit/LogicalOrderProjectionReconciliationTests.cs`，+13 tests）

captured falsifier + 8 类 counterexample + permutation property + 2 个边界门回归
（中页行不截断 / 顶带垃圾仍截断）+ determinism。

## 2. Logical-Order Projection 定义（正式）

```
给定 Observation：
  1) 收集与旧 ExtractNavigationSignatures 完全相同的 NavigationCandidate 条目
     （同一过滤器、同一 BuildSignature 身份）→ 帧内集合不变；
  2) group key = StableKey（Vision 且非空）| 该条目签名（无 StableKey / structured，
     帧内唯一）——同一逻辑行的变体表示保持邻接；
  3) 行组带 = 组内所有**有效**成员 CenterY 的最小值（该行最上探测包络）；
     无有效 bounds 的成员/组带 = +∞（排最后）；
  4) 投影序列 = (行组带 asc, elementIndex asc)；elementIndex 只做确定性 tie-break。
SERIALIZATION_ORDER != LOGICAL_UI_ORDER（投影理由）；BOUNDS_ORDERING != BOUNDS_IDENTITY
（bounds 只用于排序；SameSource identity 仍为精确结构化签名）。
```

## 3. RED→GREEN（确定性）

- **RED**（HEAD 原始 normalizer，stash 复现）：`Seq22ToSeq25_RepresentationReorderOnly…`、
  `PermutationProperty_…`、`SameText_DifferentLogicalRows_DoNotMerge` **FAIL**（3/11，
  与诊断 gate 记录的 pair 1 生产失败同签名：anchor 映射 `[3,0,1,2,...]` 非单调 → Unresolved）；
  其余 counterexample 全部通过（8/11）→ RED 有效。
- **GREEN**（修复后）：**13/13**（新增 2 个边界门回归）。
  `Seq22ToSeq25_…`：pair 1 Resolved，anchor 13 个，**inserted = 4 个新 source**
  （`row_022|menu_item`、`row_027|text_block`、`row_035|menu_item` | `row_034|menu_item`），
  union 精确 = 15+4 = 19 条投影序签名；`BoundaryTruncations` 空（anchor tier 解析）。

## 4. Counterexample Preservation（8 类 + 2 回归）

| # | case | 结果 |
|---|---|---|
| 2 | 真实 backward scroll（已见行、0 新源）| **fail-closed**（anchor 零插入拒绝；`TrueBackwardScroll_StaysUnresolved`）|
| 3 | pure repeat 虚假声称进展（非后缀切片）| **fail-closed**（`PureRepeat_NoNewRows_StaysUnresolved`）|
| 4 | 同 Y 重叠行歧义（同 StableKey+type+band → 同签名）| **fail-closed**（`SameYDupRows_SameSignature_StaysUnresolved`）|
| 4b | 同 Y 不同逻辑行 | 不被合并/不塌缩，count 保持（`SameYOverlappingDistinctRows_AreNotConflated`）|
| 5 | 同文本不同逻辑行（row_010 vs row_099）| 不 merge；自我配对（`SameText_DifferentLogicalRows_DoNotMerge`）|
| 6 | 不同滚动偏移、逻辑序不变 | 确定性：union/evidence 与未平移完全一致（`DifferentScrollOffsets_…`）|
| 7 | 同行的重复表示（tb+mi）| 分组不改 count/identity；签名零 bounds 泄漏（`DuplicateRepresentations_…`）|
| 8 | 既有 anchor-adjacent confirmation repair | 保持 GREEN（`AnchorAdjacent_…`；AnchorMergeTests 全绿）|
| 8b | 边界门回归：中页新行投影到 head | 不截断 → anchor 插入；顶带垃圾仍截断（2 新测试）|
| — | permutation property | 同逻辑源仅序列化排列变化 → 结果一致（§5）|

## 5. Permutation Property（Stability Property Suite buyer evidence 候选）

`PermutationProperty_SerializationOrderDoesNotChangeResult`：同一组 5 个逻辑行（row_010..row_050，
相异 bands），仅改变 frame2 的 **Elements 数组排列**（3 种排列），geometry/identity 证据不变 →
**normalization 结果逐项一致**（union 相等、evidence 相等、严格 overlap tier、零 anchor merge、
零 boundary truncation）。RED 时该性质不成立（数组序排列会改变结果/导致 Unresolved）。

## 6. Full Deterministic Suite / 机械检查

- `dotnet test src/UniClaw.Runtime.sln`（最终树）：**2321 passed / 6 failed（2327 total，
  含 +2 边界门回归测试）**。
- 6 个失败分类（全部与本 gate 改动无关；无我的改动时同签名复现或经隔离验证）：
  - `CORR_HOST03/04/09`（3）——vision host identity（并行未提交 observability/perception 工作
    致 config identity 漂移；无我的改动同样失败）；
  - `Capstone_OneAgentOneRun_RealEmulator`、`ExternalBoundary_RealDevice`（2）——设备/环境依赖
    （基线既有失败签名）；
  - `SameDeviceExclusivity_SecondConcurrentRejected_ReleasedAfterTerminal`（1）——DriverHost
    并发日程测试，全量并跑时 flaky；**隔离运行 3/3 通过**（非回归）。
  - 备注：本 session 早前全量 run 曾出现 `Settings*_RealDevice_Phase1-5` 失败——当时模拟器未
    boot；本 gate 复验期间模拟器在线后全部通过。
- 曾被第一版投影破坏的确定性控制测试 `QuiescenceAdmissionRedTests.S3` / `QuiescenceTerminalHandoffTests.S11`：
  **边界门修复后恢复 GREEN**（stash 归因证明破坏/恢复均随本 gate 改动出现/消失，非环境）。
- `scripts/check-consistency.sh`：**ALL PASS**（C1..C15，EXIT=0）。
- `git diff --check`：CLEAN。

## 7. Fresh Real Campaign（emulator-5554 + .venv-local-vision + validation-scoped shadow receipt `/private/tmp/p26-shadow-receipt.json`）

### round-1（13 observed frames，`/tmp/p26-normalizer-repair-r1-*`）

- root 探索链与历史 run 一致：`source-seq 2,5,8,11,14,17 → exhausted at 19`；accepted decisions
  `5,8,11,14,17,19`（attempt 2 / 19 attempt 1）。
- **trace 全链零 `Source normalization is unresolved`** —— 上一 blocker 的 terminal reason
  （`Source normalization is unresolved; completeness cannot be proven.`）在本 run **完全消失**：
  root normalization 正常 resolved（`viewport exploration exhausted` 后有断言路径直入完整性检查，
  未再出现任何 normalizer Unresolved 记录）。
- **新 first blocker（记录，不修）**：root 完整性停在
  `Unknown interaction affordances remain; completeness cannot be proven.`——本 run 的 root
  accepted 帧携带 authorization-eligible **Unknown** 元素（`seq7/8`：garbled OCR 带 `LOu` idx1 +
  无文字 idx15/18；`seq13/14`：`Safety & emergency` idx8）。这些 Unknown 由**未改动的**
  `InteractionAffordanceAnalyzer` / semantic capability 分类（帧级、设备状态相关；属已登记的
  Unknown 残余类：OCR garbling / ICON_TEXTLESS），**不属于 normalizer Owner**——本 gate 零触碰。
- 因此本 run 在进入 Display child（seq22 起）之前即停止；child pair-1 的真实帧复验由
  round-2 继续尝试（round-2 若再被同一根阻塞则如实记录，且确定性 captured falsifier
  （真实 seq22/25 bounds）已是 pair-1 的形式化证明）。

### round-2（11 observed frames，`/tmp/p26-normalizer-repair-r2-*`）— **fresh-real 证明达成**

| 观察 | 证据 |
|---|---|
| root normalization | `open-world container inventory complete: sources=8, unresolved=0, seq=[2,4]` ✅ |
| **child (Display) 进入并推进** | `viewport exploration continue: source-seq=7/10/13 → exhausted: seq=16`；acceptedViewportDecisions `SettingsSubpage(Display) CONFIRMED at 10/13/16 (attempt 2)` |
| **representation reorder 真实复现** | child 首窗 seq7：`row_010\|menu_item` 位于 canonical **pos 12**、**partial-width**（X1=0.0667, X2=0.3403, Y 0.1875..0.2269 —— 与诊断 gate 的 seq22 同 bounds）；首确认窗 seq10：`row_010\|menu_item` 位于 **pos 0**、**full-width**（X1=0.0069, X2=1, Y 0.0625..0.1194 —— 与 seq25 同形状）。空间秩均为顶行 → 与 seq22→seq25 完全同类的 representation-only reorder |
| **normalizer 不再阻塞** | child 4 窗链 [7,10,13,16] 全程 **零 `Source normalization is unresolved`**（历史 run-1 同形态 pair-1 曾在此 fail-closed 终止）；运行继续到 exhaustion 之后的完整性检查 —— 修复目标达成 |
| 新 first blocker（记录，不修） | terminal = `Unknown interaction affordances remain; completeness cannot be proven.` —— r1 在 root（`LOu` garbled OCR band seq7/8 idx1 + 无文字 idx15/18；`Safety & emergency` seq13/14 idx8）；r2 在 child（首窗 seq6 的 text_block 碎片行 `Display`/`Brightness`/`Lock display`/`Appearance`/`Display size and text`/`Color` + 多个 no-text idx —— 与已登记 `SEMANTIC_PATTERN_PREDICATE_FACT_FRAGMENTATION` / dual-representation 分类为 Unknown 同源）。这些分类来自**未改动的** semantic capability / `InteractionAffordanceAnalyzer`（在 normalizer 之前），Owner = 感知/语义覆盖层，非本 gate |

## 8. AuthorityDelta / ArchitectureDelta

- **AuthorityDelta: NONE**（无授权/权限/所有权变化；Human 已批准本 gate 范围）。
- **ArchitectureDelta: NONE（ORDER-ONLY + 1 个边界谓词）**：无新 abstraction / 无新 boundary /
  无生命周期变化；identity 契约、union/completeness 语义、单调性 guard、anchored-adjacent
  语义逐字保留；新增的 boundary plausibility 谓词是既有边界放宽的**收窄**（fail-closed 方向），
  明确文档化为 ORDERING evidence（非 identity）。
- **RuntimeBehaviorDelta: NORMALIZER-ORDER-RECONCILIATION-ONLY**——只改变
  normalization comparison projection 的顺序与边界放宽的可行性判定；
  `OccurrencesOf` / viewport stability / grounding / completeness / fusion / semantic patterns 零改动。
- 影响声明：不触及 Agent authority、FSM、Traversal、GoalEvidence；无 scenario knowledge。

## 9. Residual Blocker / Phase 2.6

- **Residual（本 gate 记录，不修）**：fresh real 的下一 first blocker 已前移到 **Unknown
  affordances（完整性检查）** —— root 端 garbled OCR / no-text 行（r1），child 端 text_block
  碎片行 classified Unknown（r2）。Owner = 感知/语义覆盖层（上游；`SEMANTIC_PATTERN_PREDICATE_FACT_FRAGMENTATION`
  与 Unknown 残余已登记同名先例），Phase 2.6 前进的下一候选 blocker 即此语义 Unknown 覆盖项；
  dual-representation 双表示 band 抖动、`Color contrast` StableKey 漂移继续由各自 Owner 负责。
- **本 gate 目标状态**：seq22/25-类 representation reorder 不再导致 `Source normalization is
  unresolved`（确定性 captured falsifier RED→GREEN + fresh real round-2 child 链零 unresolved）；
  backward / pure-repeat guard（counterexample 2/3）在投影后仍 fail-closed。
- **Phase 2.6 readiness：STOPPED**（Leader 指令：本 gate 产出不自动触发重入；下一 blocker
  （semantic Unknown 覆盖）需其自身 Owner / Human Gate 裁决后再定 Phase 2.6 后续）。

## 10. Boundary Declaration

- 未删除/放宽 monotonicity guard；未按 text 强匹配；未用未来窗口修正当前窗口；
  未修改 completeness / union / Fusion / semantic patterns；未为通过个案全局 sort elements
  （投影是每个窗口的 ORDER-ONLY 序列重构，由 invariant 意图推导）。
- 零改动：`OccurrencesOf`、`NavigationRowCenters`/`IsViewportStable`、
  `SourceGroundingValidator`（grounding 依赖 union 签名集合成员判定，排序不敏感——已验证）。