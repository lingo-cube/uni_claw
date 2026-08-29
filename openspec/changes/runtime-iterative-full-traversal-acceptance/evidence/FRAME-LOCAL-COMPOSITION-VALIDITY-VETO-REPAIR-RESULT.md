# PROJECT_LEADER_FRAME_LOCAL_COMPOSITION_VALIDITY_VETO_REPAIR_RESULT

> Gate: `PROJECT_LEADER_FRAME_LOCAL_COMPOSITION_VALIDITY_VETO_REPAIR_GATE` · 2026-08-29
> Decision: Diagnostic ACCEPTED · `FRAME_LOCAL_COMPOSITION_VALIDITY_VETO` ACCEPTED · Production Fusion repair AUTHORIZED ·
> Acceptance/dedupe NOT AUTHORIZED · cadence/column tolerance relaxation NOT AUTHORIZED ·
> Unknown/completeness/semantic changes NOT AUTHORIZED · Phase 2.6 STOPPED
> HEAD: `e6c6f4b`（工作树）

## 1. 结论速览

**C4 column-spread 修复已实施并全量验证：C4 现仅对 uniform-list provenance 行集执行
（`UNIFORM_LIST_ROW_REASONS`），relation-head band 不再因 uniform-list 单列网格前提被整组否决；
C1/C2/C3/C5 继续覆盖全部生成行；全部阈值（±14% cadence、columnToleranceFloor/Ratio、minStepRatio、cap）
零改动。**

| 验证项（Leader 要求） | 结果 |
|---|---|
| seq9 捕获几何 RED falsifier（95px > 42.4px 当前整组回滚） | ✅ RED 前提数值断言 + 真机 PRE trace（POST3）记录在案 |
| GREEN：relation-head 导航投影不得 11→1 塌缩 | ✅ 确定性 falsifier + **post-fix 真机 campaign：组合行跨 5 个滚动视口保持（13 menu_item @ 最后一帧），无 C4 veto，FDP-residual NONE** |
| 真 uniform-list 列错位仍 fail-closed | ✅ `test_genuine_uniform_list_column_misalignment_still_rejects` |
| relation-head malformed/provenance/cap/vertical-cadence 反例仍拒绝 | ✅ 4 个反例测试（C1/C3 cap/C3 provenance/C5）|
| viewport translation 角色稳定 / 一致 fail-closed | ✅ 确定性（C5 仍全组覆盖）+ 真机：子页行角色跨 seq22/23/25/26/28 稳定（menu_item 恒在）|
| targeted perception / 全感知回归 / C# build / consistency | ✅ 301 passed / 3 既有漂移失败 · build 0 errors · consistency ALL PASS |
| 不得自动重入 Phase 2.6 | ✅ Phase 2.6 维持 STOPPED（post-fix run 终态移到下一个下游 gate，见 §6）|

## 2. Minimal Code Change（单文件 + 测试）

### `operators/spacing_verifier.py`（唯一生产文件）
- 新增 `UNIFORM_LIST_ROW_REASONS = {uniform_list_bracketed_row, uniform_list_upper_continuation,
  uniform_list_lower_continuation, uniform_list_anchor_duplicate_absorbed}`（C4 推导前提成立的 provenance 集）。
- `_geometry_violation`：
  - **C4 column-spread** 改为仅对 `typeInferred ∈ UNIFORM_LIST_ROW_REASONS` 的行计算（<2 行则跳过）；
    structural title-column exemption（S2fix2）保留，但在该 C4 scope 内评估；
  - **C5 vertical cadence / C1 structure / C2 containment / C3 cap+provenance** 不变，继续覆盖**全部**
    generated 行（relation-head band 仍受全部非 C4 检查约束）；
  - veto 文案区分 scope（`uniform-list rows` / `non-exempt uniform-list rows`）。
- 未改任何阈值/参数边界；`GENERATED_ROW_REASONS`（provenance 授权集）不变。

### 测试
- 新增 `tests/test_composition_validity_veto_repair.py`（9 用例）：seq9 捕获几何 RED 前提、
  GREEN verified（relation-head 95px spread 不再 veto）、真 uniform-list 列错位仍 reject、
  对齐 uniform 行 verified、relation-head malformed bounds / unauthorized provenance / cap /
  vertical-cadence 反例仍 reject、混合帧（对齐 uniform + 宽列 relation-head）verified。
- 更新 `tests/test_spacing_verifier_title_column.py`：C4-scope 语义（exemption 在 uniform-list scope 内
  保持 fail-closed；relation-head 同形宽列 verified）。

## 3. RED→GREEN（seq9 捕获几何）

### RED（pre-repair，真机 POST3 trace 直接记录）
```
step1 row-relation-head  activated | merged 11 band head(s) …  menuItemIds=[candidate_1, band_1..band_12]
step2 spacing-verifier   fail_closed | "generated rows' column spread 95px exceeds the tolerance bound 42.4px"
                         ⇒ fail-closed rollback → menuItemIds=[candidate_1]
导航投影 11 → 1 → (7,9) multiplicity mismatch → quiescence budget exhausted
```

### GREEN（post-repair）
- 确定性：`verify(seq9 12行 geometry) → verified`（RED 前提 `spread 95 > bound 42.4` 由
  `test_red_premise_seq9_spread_exceeds_bound` 数值断言钉死）。
- 真机（post-fix C4-POST campaign，22 帧/22 trace）：子页 NOOP 帧 → relation-head activated，
  组合行**跨 5 个滚动视口保持**（seq 22/23/25/26/28 均 11–12 menu_item；最后一帧 13 menu_item，
  含 'Display'/'Dark theme'/'Color contrast'/'Other display controls'/'Auto-rotate screen'/'Screen saver'）；
  **无任何 C4 veto；FDP-residual（NOOP+fallback skipped）= NONE**。

## 4. Counterexamples（全部保持）

| 反例 | 验证 |
|---|---|
| uniform-list 真列错位 → fail-closed | `test_genuine_uniform_list_column_misalignment_still_rejects`（rejected，"column spread"）|
| relation-head malformed（C1 inverted bounds）→ reject | `test_relation_head_malformed_bounds_still_rejected` |
| relation-head unauthorized provenance（C3）→ reject | `test_relation_head_unauthorized_provenance_still_rejected` |
| relation-head cap（C3 maxMenuItems）→ reject | `test_relation_head_cap_exceeded_still_rejected` |
| relation-head vertical cadence（C5 min step）→ reject | `test_relation_head_vertical_cadence_still_rejected` |
| activated 帧不重复 fallback / 直调兼容 / 歧义帧 fail-closed | 前一 repair gate 的 7 用例继续全绿 |

## 5. 套件 / 构建 / 一致性

- 感知全量：**301 passed / 3 failed**（3 个为既有 pre-existing 漂移：detection.confidence 0.35 断言、
  reality-repair 阈值回归 ×2——与本修复无关，先于本 gate 存在）。
- C# `dotnet build`：0 errors（本修复 Python-only；C# 二进制未变）。
- `scripts/check-consistency.sh`：ALL PASS。
- S1 equivalence baseline：GREEN（corpus 无 C4-veto 帧 → 行为零变化）。

## 6. 真机验证（fresh real campaign, post-fix）

- 环境：emulator-5554 冷启动恢复（`~/.android` 过期锁为此前沙箱写限制所致，已解除）；
  shadow receipt（validation-scoped，CURRENT-ACTIVE 未动）。
- 22 帧 / 22 trace / 16 decisions / 15 dispatches / 5 次滚动。
- **quiescence budget exhaustion FDP 消除**：无 C4 veto、无投影塌缩、无 NOOP+fallback skipped。
- run 终态推进到下一个下游 gate：`Failed — "Source normalization is unresolved; completeness cannot be
  proven."`（SourceEquivalenceNormalizer 层——即既有已登记的 normalization 插入顺序类已知问题，
  非本 repair 权限范围）。
- 双表示（text_block 副本）依旧为 B 类（NonInteractive/重复表示），非阻塞源（前一诊断 gate 结论保持）。

## 7. Deltas / Phase 2.6

- **AuthorityDelta: NONE**（verifier 谓词 scope 属既有 code-owned VALIDATOR 内；零语义/授权变更）。
- **ArchitectureDelta: NONE**（无新抽象/边界；C4 scope 由 provenance 常量集表达）。
- **RuntimeBehaviorDelta: FUSION-ONLY**——仅"C4 对非 uniform-list provenance 行集的整组否决"被移除；
  其余行为逐字节不变（S1 baseline + corpus + 301 用例全绿即证）。
- **Phase 2.6 reentry readiness：仍 NOT READY / 维持 STOPPED**（Leader 指令；本修复不解锁重入）。
  当前下一阻塞：SourceEquivalenceNormalizer（unresolved，既有已登记的 normalization 插入顺序类已知问题）——
  需要其自身的 Human Gate。本 gate 产出不自动触发任何重入。

## 8. 边界声明

- 未放宽任何 tolerance（±14% cadence / columnToleranceFloor=24 / Ratio=0.20 / minStepRatio=0.15 /
  maxMenuItems=200 全部原值）；未修改 routing/settle/acceptance/CURRENT-ACTIVE；未新增 Settings pattern；
  未动 Unknown/completeness/语义层；未修 residual Unknown。
- C1/C2/C3/C5 对全部生成行的覆盖在测试中逐条钉死（4 反例 + 2 正例）。
- 真机验证使用 validation-scoped shadow receipt（身份事实来自工作树 /version；CURRENT-ACTIVE 未动）。