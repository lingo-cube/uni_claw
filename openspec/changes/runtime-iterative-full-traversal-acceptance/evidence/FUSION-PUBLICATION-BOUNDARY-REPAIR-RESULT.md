# PROJECT_LEADER_FUSION_PUBLICATION_BOUNDARY_REPAIR_RESULT

> Gate: `PROJECT_LEADER_FUSION_PUBLICATION_BOUNDARY_REPAIR_GATE`
> Date: 2026-08-30
> HEAD: `1cbe7e7`（工作树；含正常化 gate 未提交改动 + 并行未提交工作）
> Decision: **PHANTOM_FRAGMENT_ORIGIN 诊断 ACCEPTED（B. SUPPORTING_FRAGMENT）· FDP ACCEPTED ·
> 最小修复 APPROVED · textless→NI / completeness bypass / Safety/OCR 修复 NOT AUTHORIZED ·
> Phase 2.6 维持 STOPPED**
> 前置: `PHANTOM-FRAGMENT-ORIGIN-DIAGNOSTIC-RESULT.md`（我们已证：no-text fragment =
> `row-relation-head` satellite，raw 已被 band.allIds 消费、自带 headId，canonical 发布层将其
> 重复发布为独立 world object）

## 0. 一句话

在 **fusion 顶层发布边界**（`fuse_evidence` / `fuse_evidence_from_crops` 的最终
`result["candidates"]`）拦截 `row-relation-head` 的 **INTERNAL_SUPPORTING_FRAGMENT**：
满足严格 6 项 predicate（row-relation-head satellite marker + 有效 headId +
headId 解析到当前 emitted band + raw source 已被 owning band.allIds 消费 +
无独立交互证据）的 satellite 不再进入顶层 world-occurrence 投影；
satellite 仍在 operator trace / fusionStages / `_diagnostics` 中可观测。
冻结不变量第一次被代码级实现：
`INTERNAL_COMPOSITION_ARTIFACT != CANONICAL_WORLD_OCCURRENCE`；
`RAW_EVIDENCE_CONSUMED_BY_PARENT_COMPOSITION != INDEPENDENT_WORLD_OBJECT`。

## 1. Minimal Diff（Perception/Fusion 层，Python）

| 文件 | 内容 |
|---|---|
| `uniclaw_perception/fusion/publication.py`（新，+140） | 冻结不变量文档 + `internal_supporting_fragment(candidate, candidates_by_id)` 严格谓词 + `partition_internal_satellites(candidates)` 确定性分区（保持原顺序） |
| `uniclaw_perception/fusion/engine.py`（+40） | `fuse_evidence` 与 `fuse_evidence_from_crops` 两个发布点：row-stabilization 之后、`result` 构造之前 `candidates[:] = published`；suppressed id 写入 `_diagnostics["internalSatellitesSuppressed"]` |
| `tests/test_publication_boundary_suppression.py`（新，+~250） | RED→GREEN falsifier（真实 r2 child det_12/det_15 几何）+ counterexample 3-6 + 9+property+determinism |
| `tests/test_cross_ui_row_composition.py`（2 测试更新） | caption/subtitle 顶层断言改为新发布语义（顶层无 satellite；band 仍携带 consumed allIds） |

零改动：`row-relation-head` 合成语义、`SourceGroundingNormalizer`、`SettingsSemanticCapability`、
`InteractionAffordanceAnalyzer`、completeness、OCR/Pattern-5、textless fallback。

## 2. Exact Suppression Predicate（6 项全满足 → INTERNAL_SUPPORTING_FRAGMENT）

```python
INTERNAL_SATELLITE_MARKER   = "row_relation_head_satellite"
HEAD_BAND_MARKER            = "row_relation_head"
INTERACTION_EVIDENCE_ROLES  = {"toggle"}     # switch/checkbox/toggle/slider 源

1  evidence.typeInferred == "row_relation_head_satellite"   # 由 row-relation-head 产生
2  （同上字段 = 显式内部 satellite marker）
3  evidence.headId 非空
4  headId 解析到同帧已 emitted relation-head band（该 candidate.typeInferred == "row_relation_head"）
5  satellite 的 raw source id(s)（allIds+yoloId+ocrIds）⊆ owning band.evidence.allIds（已消费）
6  satellite.role ∉ {"toggle"}（无独立 primary interaction evidence）
```

任一不满足 → 保持现有 fail-closed 发布。禁止的判据（text=="" / NonInteractive type / overlap /
containment / no-clickable / same-text）均不单独参与判定（identity 非 ownership proof）。

## 3. RED→GREEN

- **RED**（未接线 engine，仅谓词就绪）：`test_exact_phantom_falsifier…`（顶层同时发布
  `relation_head_band_1` 与 `relation_head_band_1_sat_0`）、`test_property_publish…`、
  `test_positive_control…`（测试自身输入修正前）FAIL；真实几何精确复现 `[band, sat]` 双发。
- **GREEN**（接线后）：**24 tests + 22 subtests 全绿**。falsifier：band 发布、satellite 不进顶层；
  satellite 在 composition stage（fusionStages）与 `_diagnostics` 可观测；property：
  `Publish(Band+S) == Publish(Band)`（顶层投影，S 在 trace 中仍可见）。

## 4. Counterexample Preservation（gate 3–6 + 既有回归）

| case | 结果 |
|---|---|
| 2. standalone text_block（无 headId） | 继续发布（engine 级 GREEN）|
| 3. broken parent reference（headId 不可解析/目标非 band） | **fail-closed 继续发布** |
| 4. raw source 未被 band 消费（∉ allIds） | **不 suppress** |
| 5. 独立交互 child（role="toggle" switch 源） | **不 suppress** |
| 6. 仅 overlap/同文本、无 parent+consumption 证据 | **不 suppress** |
| 7. 既有 ChildOf/Search-icon 回归 | 感知全量 GREEN（含 generic composition 相关套件）|
| 8. relation-head 正常行/副标题/toggle 语义与数量不变 | 感知全量 GREEN（312 passed）|
| 9. determinism | 同输入两次发布逐项一致 |

## 5. Deterministic Regression

- **Perception/Fusion 全量**（`.venv-local-vision/bin/python -m pytest tests`）：
  **312 passed / 3 failed（与基线 301/3 同签名：test_reality_repair×2、test_server config）** —— 零新增失败。
- **C# Runtime 确定性套件**（`dotnet test src/UniClaw.Runtime.sln`）：
  **2329 passed / 5 failed（CORR_HOST03/04/09 identity 漂移、Capstone_RealEmulator、ExternalBoundary_RealDevice —— 全环境性，与改动无关）**。
- `git diff --check`：CLEAN（感知 + 证据文档 diff）。

## 6. Fresh Real Campaign（before/after deltas）

> 环境：emulator-5554 + 当前工作树感知管线 + **validation-scoped shadow receipt
> （`/private/tmp/p26-shadow-receipt-pubfix.json`，由 live server `/version` 于本 gate 现铸；
> pipelineRevision 因本 gate 代码变更而更新为 `prev:300452238e…`，model/configId 不变）**。

### BEFORE（r2，publication-fix 前，accepted Display child 帧）

| seq | canonical/fusedEvidence | 顶层 satellite | 其中无文本 | eligible Unknown |
|---|---|---|---|---|
| 7 | 33 | 9 | 6 | 13 |
| 10 | 34 | 8 | 5 | 12 |
| 13 | 42 | 16 | 13 | 21 |
| 16 | 42 | 16 | 13 | 21 |

### AFTER（r4，publication-fix 后，直达 Display child — 每 accepted 帧顶层 satellite = 0）

| seq | 容器 | canonical/fusedEvidence | 顶层 satellite | 其中无文本 | eligible Unknown | PHANTOM_UNKNOWN | GENUINE/RESIDUAL_UNKNOWN |
|---|---|---|---|---|---|---|---|
| 4 | root | 9 | **0** | 0 | 0 | 0 | 0（root `inventory complete: sources=8, unresolved=0` ✓）|
| 10 | child | 28 | **0** | **0** | 8 | **0** | 8（全部为**带 row 的 text_block 副本**：Brightness/Lock display/Not set/Appearance/Will never/Color/Color contrast/Other display controls — 语义 Pattern-5 判决变量类，非 phantom）|
| 13 | child | 26 | **0** | **0** | 7 | **0** | 7（同上）|
| 16 | child | 26 | **0** | **0** | 7 | **0** | 7（同上）|

**r3（root 侧）对照**：accepted root 帧顶层 satellite = 0（r1 同型帧曾有 band satellites）；
eligible Unknown 坍缩为单个 genuine icon（ICON_TEXTLESS 类，fail-closed 保留）。

### 验收判据（gate Acceptance）逐项

- **原 6..13 no-text satellites 不再进入独立 canonical occurrence**：✅ 全 accepted 帧
  （r3 root + r4 root/child）顶层 satellite=0；确定性 falsifier（真实 child det_12/det_15 几何）
  RED→GREEN 证明结构级移除。
- **phantom-fragment Unknown obligation = 0**：✅ r4 child 残余 Unknown 全部为文本承载的
  text_block 副本（每枚带 row_id），无任何 no-text item（`noTextNonNI=0`）。
- **band/menu_item 行仍正常存在**：✅ r4 child 每帧 13 个 menu_item（含
  `relation_head_band_0..N` 与 candidate 行），跨帧稳定；band 的 `allIds`（consumed evidence）
  完整保留（band_1 allIds=5）。
- **source count/identity 无异常缩水**：✅ root sources=8 与修复前一致；band 身份/文本不变。
- **genuine Unknown 仍 fail closed**：✅ r4 child 完整性对 text_block 副本 Unknown 保持
  fail-closed（terminal 不变，未新增 bypass）。
- **completeness 无新增 bypass**：✅ 过滤仅发生在 fusion 顶层发布边界，C# 完整性逻辑零改动。
- **`Safety & emergency` / `LOu`**：本 run（r3/r4）未出现（设备状态相关），DEFERRED 单独登记（§7）。

## 7. Residual First Blocker / Phase 2.6

- **下一 first blocker（记录，不修）**：r4 child 完整性仍停在
  `Unknown interaction affordances remain`，计数来源为**带 row 的 text_block 副本**（
  Brightness/Lock display/Appearance/… — 同帧其 menu_item peer 存在但文本副本仍被判 Unknown/
  NavigationCandidate 的撕裂）——即已登记 `SEMANTIC_PATTERN_PREDICATE_FACT_FRAGMENTATION`
  语义判决变量类；r3 root 曾出现 toolbar icon Unknown（`ICON_TEXTLESS` 类）。两者 Owner =
  语义覆盖层（Pattern-5/role 判决）与感知（icon），**不在本 gate**（Pattern-5/OCR NOT AUTHORIZED）。
  `Safety & emergency`（漏合成 menu_item 行）与 `LOu`（OCR 残块）在 r3/r4 未出现，继续 DEFERRED。
- **Phase 2.6 维持 STOPPED**；本 gate 完成后停止，等待 Human（下一候选 gate：semantic
  fragment-verdict 一致性，Owner 另行裁决）。

## 8. AuthorityDelta / ArchitectureDelta

- **AuthorityDelta: NONE**（发布边界纯 code-owned；无授权/所有权变化）。
- **ArchitectureDelta: ADDITIVE(publication seam) + 零语义放宽**：新增唯一的顶层发布过滤
  （融合 output → canonical world 的 boundary），不改变 band 组合、不改变任何 downstream 判决。
- 冻结不变量以代码形式落地：`INTERNAL_COMPOSITION_ARTIFACT != CANONICAL_WORLD_OCCURRENCE` 等。

## 9. Boundary Declaration

未删任何诊断证据（satellite 在 trace/fusionStages/operator record 中保留）；未改
row-relation-head 合成语义/OCR/Pattern-5/Unknown 语义/completeness/`SourceGroundingNormalizer`/
`SettingsSemanticCapability`/`InteractionAffordanceAnalyzer`；无 textless fallback；
未按 bounds 推断 SameSource；未全局过滤 NonInteractive；未删除普通 standalone text_block；
未触碰 `Safety & emergency` / `LOu`。