# PROJECT_LEADER_FRAME_LOCAL_FUSION_ROLE_STABILITY_REPAIR_RESULT

> Gate: `PROJECT_LEADER_FRAME_LOCAL_FUSION_ROLE_STABILITY_REPAIR_GATE` · 2026-08-29
> Decision: `Trace result: ACCEPTED` · `Fusion repair: APPROVED` · `Cadence tolerance change: NOT AUTHORIZED` ·
> `Phase 2.6: STOPPED` · 本次只实施修复，`Phase 2.6` 维持 STOPPED
> HEAD: `e6c6f4b`（工作树）

## 1. 结论速览

FDP（trace 证实）：`uniform-list` 因 cadence model 不成立返回 NOOP，`relation-head` 仍因
`confirmedAnchors >= 4` 被 count-only 委派跳过 → 完整行滞留 text_block → 帧间角色翻转。

Minimal fix 已实施（**仅二文件**）：ownership 由**实际组合成功**决定（activated → 委派；NOOP → fallback 尝试），
fallback 在**高 anchor 帧**上受 **anchor cadence envelope** 约束（同一组 pitch/column/tolerance 常量，
**未放宽 ±14%**），硬约束"永不发明超过已确认数量的行"；无 cadence 参照的帧维持 fail-closed。

验证结论：

| 项 | 结果 |
|---|---|
| RED→GREEN（seq4/5 已捕获几何，确定性 falsifier 7 用例） | ✅ 全绿 |
| 感知全量套件 | 290 passed / 3 既有漂移失败（S1 equivalence baseline **恢复 GREEN**，零回归）|
| C# build / 全量 | 0 errors · 2293/10（同既有基线，本次 Python-only）|
| consistency | ALL PASS |
| fresh real campaign（post-fix，6 帧/6 trace）| **FDP-residual（NOOP + fallback skipped）= NONE**；uniform-list NOOP 帧 → relation-head ACTIVATED；'Display'/'Dark theme' 组成 menu_item |
| 翻译稳定性（seq6↔seq7↔seq9，Display 子页）| 每行 menu_item 角色跨帧保持；残留为同帧文本副本（pre-fix 亦如此，语义层 Pattern-5/稳定键承接）|

---

## 2. Minimal Code Change（已批准范围，Fusion-only）

### `operators/relation_head_router.py`（核心）
`run_row_relation_head_routed(...)` 路由规则改为：

1. `previous_generator_decision.status == "activated"` → **delegated**（激活路径与 S1 输出逐字节不变；
   reason = `delegated: uniform-list-row-grouping activated (composed this frame) …`）。
2. 直调（无 pipeline 上下文，`previous_generator_decision=None`）→ 保留旧 count-only 委派（兼容，字节不变）。
3. uniform-list **NOOP** 且该帧为**高 anchor 帧**（`confirmedAnchors >= ROUTING_MIN_ANCHORS`）：
   - 由 anchors 推导 cadence 参照（`_cadence_envelope`：median(下 60% gap)=pitch、gap 严格多数对齐、
     median(x1)=列、同一 `cadenceTolerance/maxCadenceSteps/xToleranceFloor/xToleranceRatio` 常量）；
     若 anchors 无多数对齐 cadence（真不规则帧）→ `fail-closed: fallback scope refused …`（歧义保持 fail-closed）。
   - 运行 relation-head 后，仅合并 **cadence envelope 内** 的候选（`_in_cadence_envelope`：centerY 距某 anchor
     为 k×pitch(k=1..4,±14%) + x1 在 anchor 列容差内）；
   - `len(acceptable) > len(anchors)` → 整体拒绝（`fail-closed: … inference bound; no rows invented`——
     对应 uniform-list 硬不变量"永不发明超过已确认数量的行"）。
4. **低 anchor 帧**（< ROUTING_MIN_ANCHORS）→ 既有 unfiltered 路径（不变；子页组合工作如旧）。

### `operators/trace.py`（管线传参）
`execute_pipeline` 把前一 GENERATOR 的 decision 以可选 keyword `previous_generator_decision` 转发给
`handles_raw_sources` runner（仅 relation-head；可选参数，直调不传 → 走兼容路径）。纯读数，零行为影响。

### 未改动（Do NOT 清单）
±14% cadence tolerance、YOLO/OCR、语义 pattern、Unknown/completeness、已确认行改写（same-line
duplicate suppression 保证）、routing/验证器顺序、修 baseline。

## 3. Operator Trace — Before / After

### Before（pre-repair，真机 v2 run，FDP 帧 seq4/5）
```
uniform-list-row-grouping noop | cadence model not inferable (…)  confirmedAnchors=7  gaps=[128,158,149,158,154,461]
row-relation-head          noop | delegated: >= 4 confirmed anchors — … (SKIPPED)
FusionOutput unresolved: 'Sound & vibration' / 'Display' / 'Dark theme, font size, brightness'  (text_block)
```

### After（确定性 falsifier，同一捕获几何 — 即产线同输入）
```
uniform-list-row-grouping noop | cadence model not inferable (…)  confirmedAnchors=7
row-relation-head          activated | composed 9 navigation candidate(s) across 9 band(s)…
      merged 'Sound & vibration' + 'Display'（envelope 内）；'Dark theme…'（off-cadence 副标题）保持 text_block
FINAL rows: 9 menu_items（含 2 个原滞留完整行）· confirmed anchors 未被改写
```

### After（fresh real campaign，post-fix，Display 子页 frame）
```
uniform-list noop (cadence model) → row-relation-head activated (composed 11-12 …)
FINAL: 'Display' menu_item(row_010) · 'Dark theme' menu_item(row_023) · 'Brightness'/'Lock display'/… menu_item
FDP-residual（uniform-list NOOP + relation-head delegated）: NONE（6/6 帧扫描）
```

## 4. RED→GREEN falsifier

`platforms/perception/tests/test_fusion_role_stability_repair.py`（新，7 用例，使用 v2 seq4/5 捕获几何）：

- `test_fdp_frame_uniform_list_noop_precondition` — RED 前置（uniform-list NOOP，7 anchors）。
- `test_fdp_frame_relation_head_not_skipped_composes_rows_green` — **GREEN**：'Sound & vibration'/'Display'
  组合为 menu_item；'Dark theme…' 副标题保持 text_block（一致 fail-closed）；已确认 anchors 不被改写。
- `test_activated_frame_still_delegates_fallback_absent` — 反例 1：activated → 委派，无 fallback 改写。
- `test_irregular_frame_stays_refused_ambiguity_fail_closed` — 反例/冻结安全：不规则帧 'Static information'
  不被提升（fallback scope refused）。
- `test_invention_bound_never_exceeds_confirmed_rows` — 反例/冻结安全：envelope 对齐行 > 已确认数 → 整体拒绝。
- `test_direct_router_invocation_keeps_count_only_delegation` — 直调兼容。
- `test_uniform_list_noop_greater_than_floor_not_delegated` — 修复规则：NOOP（≥4 anchors）不再 count-委派。

配套更新（追踪旧 count-only 语义的测试改为新语义）：
`test_fusion_causal_trace.py`（2 用例）、`test_stage_trace_observability.py`（fixture 改为 cadence-valid 激活帧）。

## 5. Translation Stability（反例 3）

post-fix 真机帧：root 行（row_002..row_009）恒 `menu_item`（STABLE）；Display 子页行（row_010..row_024）
在 seq6/7/9 三次视口平移中**菜单角色恒存在**（menu_item），同帧残留的 text_block 为 raw fused 副本
（pre-fix 子页亦如此，属既有双表示；语义层经 Pattern-5 重复行/稳定键解析）。FDP 前该角色在 NOOP 帧
**缺失**（只有 text_block）——修复后角色跨帧保持，即"viewport translation 后同一完整 row role 保持稳定"达成。

## 6. Fresh Real Campaign — Residual Unknowns

post-fix run 终态：`Failed — quiescence admission budget exhausted (last seq=9, multiplicity mismatch …)`
（ViewportAcceptance 层未能接纳稳定帧——双表示导致 count/签名帧间微变 + 语义层 Unknown 残余）。
这属于 **ViewportAcceptance/Semantic 层**残余（含 ICON_TEXTLESS 等已登记 gate），**不在本 repair 权限内**
（Do NOT: 修改 Unknown/completeness）——如实登记，不修。

Fusion 侧本次修复目标（NOOP+fallback skipped 消失、完整行→menu_item）在真机帧上达成。

## 7. Authority / Architecture / RuntimeBehavior Delta

- **AuthorityDelta: NONE**（路由规则在既有 code-owned 框架内；零语义/授权变更）。
- **ArchitectureDelta: NONE / ADDITIVE（仅路由决策面）**：无新抽象/新边界/生命周期变更；`relation-head`
  的 fallback 在既有 GENERATOR+VALIDATOR 链内，输出继续过 spacing-verifier/text-relation-check/
  structured-corroboration（实测 `verified …`）。
- **RuntimeBehaviorDelta: FUSION-ONLY**：仅"uniform-list NOOP 且 ≥4 anchors 且 envelope 内有剩余行"的帧
  输出改变（多组合出完整行）；其余帧（activated / 低 anchor / 不规则帧 / 无 previous 直调）**逐字节不变**——
  S1 equivalence baseline 与 navigation/composition 冻结套件保持 GREEN 即证。
- 未启用任何新配置/规则集；未重生成任何 baseline artifact。

## 8. Phase 2.6 Reentry Readiness

- `FRAME_LOCAL_FUSION_ROLE_STABILITY_REPAIR_GATE`：**满足**（RED→GREEN + 全套件 + 真机 trace 无 FDP 残余）。
- 但 **Phase 2.6 维持 STOPPED**（Leader 指令）：残余阻塞仍在 ViewportAcceptance（quiescence multiplicity
  双表示）+ Semantic 层（icon/text_block Unknown）——均在本 repair 权限外，需各自 Human Gate。
- 下一候选 gate：`VIEWPORT_ACCEPTANCE_DUAL_REPRESENTATION_GATE`（或由 Leader 直接裁决
  Phase 2.6 残余项归属）。

## 9. 边界声明

- 未放宽 ±14% cadence tolerance；未修改 detector/OCR；未修改 semantic patterns；未修改
  Unknown/completeness；未让 relation-head 重写已确认行（same-line suppression + envelope 双保险）。
- 全程零改变：RPER-6 列（adaptation 层）未触碰；shadow receipt（validation-scoped）仅用于绕过并发工作树
  与 CURRENT-ACTIVE 的 config 分叉，CURRENT-ACTIVE 未动。
- 环境注意：post-fix 真机验证期间 emulator-5554 崩溃一次（环境故障），已冷启动恢复后重跑成功。