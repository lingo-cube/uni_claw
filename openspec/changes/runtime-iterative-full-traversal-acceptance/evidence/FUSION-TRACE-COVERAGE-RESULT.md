# PROJECT_LEADER_PERCEPTION_FUSION_TRACE_COVERAGE_RESULT

> Gate: `PROJECT_LEADER_PERCEPTION_FUSION_TRACE_COVERAGE_GATE` · 2026-08-29
> Decision from Leader: `FRAME_LOCAL_FUSION_INSTABILITY: ACCEPTED` · `Fusion repair: NOT AUTHORIZED` ·
> `Perception/Fusion Trace coverage: APPROVED` · `Phase 2.6: STOPPED`

## 1. 结论速览

Trace 已落地并在 **fresh real campaign（emulator‑5554，真实 Android Settings）** 上采集到 11 帧/11 条
fusion causal trace（含 8 帧滚动后视口 + 前导帧）。Gate 候选假设被**真机 trace 直接证实**：

```
confirmed anchors (7) ≥ 4
→ uniform-list NOOP: "cadence model not inferable (insufficient or irregular anchor geometry)"
→ relation-head SKIPPED: "delegated: >= 4 confirmed anchors — uniform-list owns composition"
→ 完整行 'Sound & vibration' / 'Display' / 'Dark theme, font size, brightness' 停留 text_block
→ 帧内 text_block ↔ 帧间 menu_item 类型漂移（seq 4/5 vs seq 7+）
→ 语义层 Unknown → "Unknown interaction affordances remain" → completeness 阻塞（本次 run 终态）
```

- **Exact first failed predicate（真机帧 seq 4/5）**：`uniform_list_row_grouping._infer_model`
  `len(direct) < 2 or len(valid) != len(gaps) → return None`（L527‑528）——首个 anchor 间距
  **128px 违反 ±14% cadence 容差**（pitch≈151.5，`|128 − k·151.5|` 越界；同帧 461px 双间距被 3×pitch
  校验命中但在 `direct` 上不成立），整帧拒绝组合。几何数据直接来自 trace 的 `anchorGeometry`（无代码反推）。
- **Owner / GapKind**：Perception/Fusion 行组合层（uniform-list cadence 模型 + relation-head 路由器
  count-only 委派）。`FRAME_LOCAL_FUSION_INSTABILITY`（Leader 已接受类），机制现由 trace 证实。
- **Fusion Repair Gate**：满足候选条件 → 输出 `FRAME_LOCAL_FUSION_ROLE_STABILITY_REPAIR_GATE`
  **候选**（不实施修复——NOT AUTHORIZED）。
- **AuthorityDelta: NONE · RuntimeBehaviorDelta: NONE**（trace 零行为影响；见 §6）。

## 2. Trace Schema / Events Added（approved 范围，全部 trace-only）

新增/扩展（`platforms/perception/uniclaw_perception/`）：

| 位置 | 内容 |
|---|---|
| `fusion/causal_trace.py`（新） | 紧凑 ref 工具 + `FusionTrace` 事件（`InputRefs` → `RouterDecision/OperatorAttempt/OperatorResult/ValidatorDecision` → `PostPipelineDiagnostics` → `RowStabilization` → `FusionOutput`）+ `first_failed_composition_decision()`（verdict 纯函数）+ `strip_stage_views()`（去 stage 大视图，只留决策/refs） |
| `operators/trace.py` | 每个 step 增补：`stepIndex`、`decisionInputs`（`confirmedAnchors`/`titleTextBlockIds`/`minAnchors`/`routingFloor`/`anchorGeometry{gaps,centerYs,titleHeights,x1s}`）、`outcomeRefs`（`menuItemIds`/`unresolvedTitleIds`）、`emitted`；仅在 `capture_candidate_views`（trace 模式）下附加，默认 trace 字节不变 |
| `fusion/engine.py` | `_run_operator_pipeline` 返回 trace dict；`_attach_fusion_trace` 组装 `fusion` 文档（events + verdict）经 `trace_sink` 提交；两个 fuse 路径一致 |
| `server.py` | `X-Perception-Trace: 1` 头 → 响应携带紧凑 `trace`（`strip_stage_views` 后）；缺省响应字节不变 |
| `LocalVisionPerceptionSource.cs` | `EmitTrace` + `LastTrace`（transport 层 trace 透传透存，Runtime 决策不读） |
| `SettingsCampaignProgram.cs` | `P26_FUSION_TRACES` 落盘每帧 trace（按 sequenceNumber 与 frames 对齐）；`P26_CAPTURE_STAGE_VIEWS` 既有 stage evidence 通道保留 |
| `tests/test_fusion_causal_trace.py`（新） | 6 用例：trace≠输出、cadence-fail 首断、anchor<4 fallback 运行、partial→router 跳过、resolved 帧否定、FusionOutput refs+rowId |

Trace 不变量（TRACE != CONTROL / EVIDENCE AUTHORITY / SEMANTIC ADMISSION）：全部读取纯函数；
`strip_stage_views` 只投影。fusion/routing 行为、Unknown/completeness、Settings pattern **零改动**。

## 3. Blocker Causal Trace（真机帧，直接由 Trace 读出）

### 3.1 现场（fresh real campaign，`settingscampaign 1 --depth 1`，真实 Android Settings 根页）

- frames：seq 1/2（根页初始）、seq 4/5（首滚后）、seq 7/8/10/11（二滚后）、seq 13/14/16（三滚后）。
- 终态：`Failed — "Unknown interaction affordances remain; completeness cannot be proven."`（当前已知阻塞源）。

### 3.2 seq 4/5（首滚后）—— 决定性拒绝帧（复现要求元素：**Display** 与 **Dark theme…** 均在此帧为未组合行）

```
uniform-list-row-grouping  status=noop
  detail = fail-closed: uniform-list cadence model not inferable (insufficient or irregular anchor geometry)
  decisionInputs.confirmedAnchors = 7   (> minAnchors 4 与 routing floor 4)
  decisionInputs.anchorGeometry = {gaps:[128,158,149,158,154,461],
                                   heights:[57,31,37,29,35,34,36],
                                   x1s:[127,126,128,128,130,125,126]}
  outcomeRefs.unresolvedTitleIds = [candidate_25, candidate_28, candidate_29, …]
row-relation-head          status=noop
  detail = delegated: >= 4 confirmed anchors — uniform-list-row-grouping owns composition (…)   ← count-only 委派
verdict = {found:true, stepIndex:0, operator:"uniform-list-row-grouping",
           reason:"fail-closed: uniform-list cadence model not inferable (…)",
           unresolvedAfter:[candidate_25, candidate_28, candidate_29],
           fallback:{checked:true, available:true, operator:"row-relation-head", skipped:true,
                     reason:"delegated: >= 4 confirmed anchors — …"}}
FusionOutput.unresolvedOccurrences =
   candidate_25 "Sound & vibration"            (n_y1 0.748)
   candidate_28 "Display"                      (n_y1 0.858)   ← 要求复现元素
   candidate_29 "Dark theme, font size, brightness" (n_y1 0.889) ← 要求复现元素
```

同一帧里已被组合的行（`menuItemIds` 含 'Recent apps, default apps'/'Notifications'/'Battery'/'Storage'/
'Security & privacy' 等）与未组合行并存——**帧内局部不稳定性**直接可见。

### 3.3 对照帧（同一页面，不同滚动位置）—— cadence 模型可推断时全部组合

- seq 7：gaps `[157,153,152,310,164,145]`（310≈2×pitch，全部命中 14% 容差）→ `activated`（recovered 1）→ `unresolved=[]`。
- seq 10/11：gaps `[158,462,151,312]`（462≈3×pitch、312≈2×pitch）→ `activated`（recovered 3）→ `unresolved=[]`。

⇒ 同一物理行在 seq 4/5 为 `text_block`、seq 7+ 为 `menu_item`：**类型漂移是帧本地的**，
且完全由该帧 anchor 几何是否通过 cadence 校验决定——与 `FRAME_LOCAL_FUSION_INSTABILITY` 判定一致，
现在有了 trace 级因果。

## 4. Exact First Failed Predicate（源码定位 + 几何证实）

| 项 | 值 |
|---|---|
| 运算符 | `uniform-list-row-grouping`（`operators/uniform_list_row_grouping.py`） |
| 函数 | `_infer_model(anchors, p)` |
| 失败谓词 | `if len(direct) < 2 or len(valid) != len(gaps): return None`（**L527‑528**）→ `_NOOP_MODEL` |
| 触发数据（seq4/5） | gaps `[128,158,149,158,154,461]` → pitch=median(排序下 60%)=**151.5**；`valid` 校验 `|g − k·pitch| ≤ 0.14·k·pitch`：**gap 128 不命中任何 k=1..4**（|128−151.5|=23.5 > 0.14·151.5=21.2）→ valid=5/6 → 拒绝 |
| 次生谓词（fallback 被跳过） | `relation_head_router.run_row_relation_head_routed` L115：`len(_confirmed_rows(candidates)) >= ROUTING_MIN_ANCHORS(4)` —— 只看"已确认行数"，**不感知 uniform-list 刚拒绝组合** |
| 直接后果 | 唯一仍可从 raw 区域组合的生成器（relation-head）被委派跳过 → 7 个已确认 anchor 之外的行全部滞留 `text_block` |

（对比 seq7/10：无 128 类越界 gap → `valid == len(gaps)` → 模型成立 → activated。）

## 5. Owner / GapKind

- **Owner**：Perception/Fusion 行组合层——`uniform-list-row-grouping` cadence 模型（主）+ 
  `row-relation-head` 路由委派谓词（次，fallback 盲点）。非 Runtime、非 SemanticCapability、非 YOLO/OCR。
- **GapKind**：`FRAME_LOCAL_FUSION_INSTABILITY`（Leader 已接受类）——现经 trace 证实为
  **组合决策帧内漂移**：同一页不同帧的 anchor 几何通过/拒绝 cadence 容差 → menu_item/text_block 帧间翻转。
- **不是**：不是 cap 越界（`_NOOP_CAP` 未触发）、不是 anchor 数量不足（7 ≥ 4）、不是验证器 veto
  （spacing-verifier `verified 5 generated row(s)`，text-relation-check/structured-corroboration 均 verified）。

## 6. AuthorityDelta / RuntimeBehaviorDelta

- **AuthorityDelta: NONE**——零语义/授权/Runtime 决策变更。
- **RuntimeBehaviorDelta: NONE**——`test_fusion_causal_trace.py::test_trace_does_not_change_fusion_output`
  及全感知套件证明 trace 开启前后 fusion 输出逐字节一致；`test_server` 等既有用例不受影响。
- 数据面：感知测试 283 passed / 3 failed（**3 个为既有 pre-existing 漂移**：`detection.confidence` 断言
  0.35 vs 当前 0.20、reality-repair falsification 阈值回归——与本次改动无关）；C# 全量 2293 passed /
  10 failed（**与既有基线同批环境类**：4 Adaptive + Capstone + ExternalBoundary + 3 VisionHost identity + 
  SettingsStrategyBinding 并发工作测试）。build 0 errors。
- 本次新增：`causal_trace.py`、`test_fusion_causal_trace.py`；修改：`operators/trace.py`、
  `fusion/engine.py`、`server.py`、`LocalVisionPerceptionSource.cs`、`SettingsCampaignProgram.cs`。

## 7. Stage Evidence Reference Linkage

| Stage | Artifact | Refs |
|---|---|---|
| 截图/raw YOLO/OCR 全量 | /v1/analyze `yolo`/`ocr`/`candidates`（既有 evidence）· `P26_CAPTURE_STAGE_VIEWS` 通道（stageViews） | Trace `InputRefs.yoloIds/ocrIds` |
| Fusion 决策链 | Trace `steps`（decisionInputs/outcomeRefs/stepIndex）+ `fusion.events` | 命中 `candidate_N` 与 evidence id 关联 |
| 行身份 | Trace `RowStabilization.rowIds` + `FusionOutput.outputRefs.rowId` | `row_00X` |
| 帧对齐 | `P26_FUSION_TRACES`（本 gate）/tmp/p26-fusion-trace-v2-traces.json ↔ `P26_FRAMES` /tmp/p26-fusion-trace-v2-frames.json（同 sequenceNumber） | seq 对齐 |
| Run 终态 | campaign JSON terminal `Unknown interaction affordances remain` | 与 §4 决策链直接因果 |

大体积 stage 数据（candidate stage views / raw arrays）不进入 Trace（`strip_stage_views` 证实 refs 保留、
视图剥离）。

## 8. Fusion Repair Gate / Phase 2.6 Next Gate

- **候选达成**：真机 trace 定位到具体谓词 → 输出 **`FRAME_LOCAL_FUSION_ROLE_STABILITY_REPAIR_GATE` 候选**。
  Candidate 修复方向（**仅候选，未授权实施**，供 Leader 裁决）：
  1. router 委派条件从"count-only"改为"uniform-list 实际激活且未留下未组合 title"（delegated 仅在
     uniform-list 成功时）——让 relation-head 在 ≥4 anchor 但 cadence 拒绝的帧上照常兜底组合；
  2. 或校正 cadence 容差/消除 128px 类边界 gap 噪声（属 uniform-list 模型参数，需规则/契约层面裁决）。
- **INSUFFICIENT_TRACE_EVIDENCE 未触发**：本次已从 Trace 直接回答 gate 问题（§3/§4），无需修复 Fusion
  之外的部分。
- **Phase 2.6 下一 gate**：`FRAME_LOCAL_FUSION_ROLE_STABILITY_REPAIR_GATE`（修复裁决）→ 通过后回归
  `CHECKBOX/ICON` 残余语义 gate → Phase 2.6 Reentry。**当前维持 STOPPED。**

## 9. 边界声明

- 未修改 routing/fusion 行为；未修改 Unknown/completeness；未新增 Settings pattern；Trace 数据未进入
  Runtime 决策路径。Fusion repair 未实施。
- 运行注意：本次 campaign 使用 **validation-scoped 工作树 shadow receipt**
  （/tmp/p26-shadow-receipt.json，身份事实来自工作树 server /version；CURRENT-ACTIVE 未动）绕过
  并发工作导致的 CURRENT-ACTIVE config 与工作树分叉——这是既存分叉的绕过，不是权威变更。