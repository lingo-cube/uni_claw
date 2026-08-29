# PROJECT_LEADER_CHECKBOX_RAW_VISION_TO_SEMANTIC_TRACE_DIAGNOSTIC_RESULT

> Gate: `PROJECT_LEADER_CHECKBOX_RAW_VISION_TO_SEMANTIC_TRACE_DIAGNOSTIC_GATE`
> Type: **Evidence Collection / Diagnosis Only** · 2026-08-29 · 零代码修改
> HEAD: `e6c6f4b` (uni-agent working tree)

## 0. Summary

真实 failing checkbox 证据链已沿
`Raw Frame → YOLO → normalization → stabilization → fusion → ObservedElement → SemanticObservationFact → LocalControl → Unknown`
完整重建。三项结论先给 Leader：

1. **YOLO vocabulary 包含 checkbox — CONFIRMED**（ACTIVE `DEKI_YOLO_RAW_V1` 同时含 `CheckBox` 与 `Checkbox`，另有
   `CheckedTextView` / `Remember` 别名折叠到 `checkbox`；label 归一化 `checkbox` 位于
   `DEFAULT_INTERACTIVE_LABELS`，fusion 不丢弃）。
2. **failing frame 的 raw detection — canonical 层 CONFIRMED，raw-model 层 INSUFFICIENT_EVIDENCE**：
   seq 4/5 元素 idx 3 = `type:"checkbox"`, `text:"INCLVVUIIIILCIIICL"`, `row_id:"row_009"`（帧证据已找到，
   见 §3.3）。但该帧的**原始 YOLO class 名 / confidence 未被采集**（campaign 未开 `capture_stage_views`），
   因此 `CheckBox` vs `Checkbox` vs `CheckedTextView` vs `Remember` 无法确定。
3. **该 checkbox 在语义层被判为 NonInteractive（Pattern 5 同帧同文本重叠 menu_item 重复行），不是 Unknown**；
   FAULT-CONTAINMENT-RESULT §15/16 里 "checkbox → Unknown?" 的假设**被确定性源码级追踪否定（未证实）**。
   同一帧实际阻塞的 Unknown 是**无文字 icon（idx 0）**（ICON_TEXTLESS_PATTERN_GATE，已在上一个 gate 记录）。

另发现一个**未登记回归**：`PhysicalEnvironment.cs` 的适配器层类型归一化 `NormalizeType("checkbox"→"toggle",
"switch"→"toggle")` 在 commit `e2d8dd4`（DSH UniFlow 工具链 commit）被删除，对应感知契约测试
`test_rper_06_canonical_switch_to_toggle_propagation` **当前 RED**（实测）。这正是"checkbox 永远无法成为
LocalControl"的编排级断点（详见 §5/§7）。

---

## 1. Evidence Inventory（证据清点）

| 证据 | 位置 | 内容 |
|---|---|---|
| 当前 ACTIVE 身份 + classVocabulary | `platforms/perception/governance/artifacts/current-active-identity.json` | labelSpaceId=`DEKI_YOLO_RAW_V1`，含 `CheckBox`、`Checkbox` |
| ACTIVE model manifest | `platforms/perception/governance/artifacts/model-manifests/16daf84f….json` / `3f39b0d6….json` | 同上 vocabulary；modelName `android_ui_detection_yolov8` |
| YOLO label 别名表 | `platforms/perception/uniclaw_perception/yolo/labels.py` | `checkbox/checkedtextview/remember → "checkbox"` |
| 检测阈值 | `platforms/perception/config/label-mapping.json` | `detection.confidence=0.20`（S2fix7 0.35→0.20） |
| failing 帧（FC campaign dump） | `/tmp/p26-frames.json`（FC run 的 P26_FRAMES 输出；11 帧 seq 1/2/4/5/7/8/10/11/13/14/16） | seq 4/5: idx 3 checkbox `INCLVVUIIIILCIIICL` row_009 |
| FC run 终态 | `/tmp/p26-fc-r1.json` | round 0 terminal=`Failed` / `Unknown interaction affordances remain; completeness cannot be proven.` |
| 前序 gate 结果 | `…/evidence/FAULT-CONTAINMENT-RESULT.md` | CHECKBOX_GARBLED_TEXT_GATE 定义；"checkbox → Unknown?"（有问号，未证实） |
| 前序 DE 诊断 | `…/evidence/DE-ADMISSION-DIAGNOSTIC-RESULT.md` | D/E 修复；静默 catch 诊断点已加 |
| 管线全景 | `…/evidence/PIPELINE-FLOW-DIAGRAM.md` | YOLO/OCR/fusion/稳定化/Admission/归一化/完整性流程图 |
| 感知契约测试（RED） | `platforms/perception/tests/test_reality_repair.py::test_rper_06` | 断言 C# 适配层含 `"switch" => "toggle"` 与 `"checkbox" => "toggle"` |

## 2. 问题一：当前 YOLO vocabulary 是否包含 checkbox — CONFIRMED

- ACTIVE（CURRENT ACTIVE，`deploy:60c84225…` / `config:edb7ad546…` / `modelId 3f39b0d6…`）classVocabulary 含
  `CheckBox` 与 `Checkbox`（两个拼写都有），另含 `CheckedTextView`、`Remember`。
- `labels.py::YOLO_LABEL_ALIASES`：`checkbox→checkbox`、`checkedtextview→checkbox`、`remember→checkbox`；
  `normalize_yolo_label` 小写化后 `CheckBox/Checkbox → checkbox`。
- Fusion 侧 `DEFAULT_INTERACTIVE_LABELS` 含 `"checkbox"` → checkbox 检测进入候选流；`heuristics._ROW_WIDGET_LABELS`
  将其视为行控件锚（不会被吸收为另一行的 title）；`apply_toggle_inference_heuristic` 不反噬已类型化的 checkbox。
- **结论：vocabulary 层面无缺口。** checkbox 可达 ObservedElement（帧证据证实 PerceptionType 落到 `checkbox`）。

## 3. 问题二：failing frame 的 raw detection 到底是什么

### 3.1 已确认（canonical/观感层）

FC campaign（`settingscampaign 1`，真实 emulator-5554 Android Settings，`SettingsCampaignProgram`）seq 4 与 seq 5：

```
idx 2  { text:'INCLVVUIIIILCIIICL', type:'menu_item', row_id:'row_009',
         bounds:(0.1750,0.1475)-(0.5792,0.1819) }
idx 3  { text:'INCLVVUIIIILCIIICL', type:'checkbox', row_id:'row_009',
         bounds:(0.0792,0.1475)-(0.6264,0.1800) }
idx 0  { text:'', type:'icon', row_id:None, bounds:(0.0944,0.0925)-(0.1389,0.1125) }
```

- **同一物理行 `row_009` 被 fusion 拆成两个 candidate**：menu_item（文本区）+ checkbox（更宽的框，含图标列）。
- 物理行身份：seq 2 中该行是 `'Network & internet'` menu_item（Y1=0.3525），滚动一步（~0.205 归一化，
  对应 InitialStep 0.6 首滚）后出现在顶部 band Y1=0.1475；seq 2 structured 层有 `Network & internet`
  (clickable LinearLayout)，seq 4/5 structured 层该行消失（顶部边界位）。
- 因此 `row_009` = 真机 Settings 根页第一行 **`Network & internet`**（导航行，非真实 checkbox）；
  `checkbox` 类型是该行区域的 **YOLO 分类伪影**（框跨图标+文本，wide box），文本被 OCR 读成
  `INCLVVUIIIILCIIICL`（顶部边界 OCR 乱码，属于感知质量症状，不是根因本身）。

### 3.2 未确认（raw-model 层）— INSUFFICIENT_EVIDENCE

- `_run_pipeline` 只有在 `capture_stage_views=True`（evaluation L2 runner）时才回传
  `rawModelDetections[].rawLabel`；campaign 路径未开启该开关。
- 故该帧的原始 YOLO class（`CheckBox` / `Checkbox` / `CheckedTextView` / `Remember`）与 confidence
  **没有任何落盘证据**。canonical `checkbox` 由 frames dump 证实，raw label 不可证。

### 3.3 误判说明

FAULT-CONTAINMENT-RESULT §16 中 "ExpectedRole = LocalControl" 本身**缺少现实依据**：`Network & internet`
是导航行（structured clickable LinearLayout + seq 2 menu_item），真实世界该处无 checkbox 控件；正确期望角色是
NavigationCandidate（行本体 idx 2 已如此获得）。"checkbox 期望 LocalControl"是把感知伪影当真控件的推断。

## 4. 证据链（逐级，全部源码/语料支撑）

| Stage | 该 failing checkbox 发生了什么 | Symbol / 语料证据 |
|---|---|---|
| ① Raw Frame | 视口 F4/F5：Settings 根页滚动后，顶部 band（Y≈0.148–0.18）渲染第一行 `Network & internet` 边界态 | `/tmp/p26-frames.json` seq 2 vs 4/5；`AdbScreenshotSource` 1080×1920 |
| ② YOLO | 检测出 canonical `checkbox` 的 wide box（raw ∈ {CheckBox,Checkbox,CheckedTextView,Remember}，别名折叠），conf ≥ 0.20 | `yolo/inference.py`；`yolo/labels.py`；`current-active-identity.json`；`label-mapping.json` conf=0.20 |
| ③ Normalization | 别名归一化 `→ "checkbox"`；full-image RapidOCR token 经 `match_score`（≤0.055·对角线）被绑到该框 → 乱码 `INCLVVUIIIILCIIICL` | `labels.py`；`fusion/engine.py::fuse_evidence`（`primary_line_text`） |
| ④ Stabilization | Python 行稳定器给两个 candidate 同标 `row_009`（同文本+同位置 band）；C# 侧 `RowIdentityContext` 保留 Python row_id | `fusion/row_stabilizer.py`；`SettingsCampaign/RowIdentityContext.cs`；frames `row_id` 字段 |
| ⑤ Fusion | checkbox candidate 保留（`DEFAULT_INTERACTIVE_LABELS` 含 checkbox；F2 契约"控件永不提升为 menu_item"）；同一文本 band 另有 operator 管线组成的 menu_item | `fusion/engine.py`；`fusion/heuristics.py`；`test_cross_ui_row_composition.py::test_toggle_never_becomes_a_menu_item` |
| ⑥ ObservedElement | idx 3 `PerceptionType="checkbox"`，`SwitchState=null`（campaign 构造 `PhysicalEnvironment` **未传** `visualControlFactory` → 无视觉状态读取），`StableKey=row_009` | `PhysicalEnvironment.cs` L110–165；`SettingsCampaignProgram.cs` L113–118 |
| ⑦ SemanticObservationFact | Projector 产出 Text + ClassName("checkbox") + Geometry；**无 BooleanState fact**（`SwitchState is null` → line 59 门控跳过）→ 全元素 `PrimitiveState=null` | `SemanticObservationFactProjector.AddVisionFacts` L52–71 |
| ⑧ LocalControl 判定 | Pattern 4：`IsLocalControl` F（provider "checkbox" ∉ {toggle,switch} 且 PrimitiveState null）；`IsToggleShape` 佐证 F（根页 structured 层无 checkable 节点，无 class 含 "Switch"，无 "toggle" provider）。**Pattern 5 命中**：同文本 `INCLVVUIIIILCIIICL` + bounds 重叠的 menu_item peer（idx 2）唯一 → **NonInteractive（重复行）**。idx 2 走 Pattern 6 → NavigationCandidate | `SettingsSemanticCapability.cs` L96–125（Pattern4/5/6）、L261–314（IsLocalControl/IsToggleShape/IsDuplicatePrimaryRowRendering）；frames seq4/5 structured（无 checkable/无 Network&internet） |
| ⑨ Unknown | 该 checkbox **不产生 Unknown**（NonInteractive 非义务）。同一帧 idx 0 无文字 icon：Patterns 1–7 全部因无文本跳过 → 无证据 → **Unknown**；无 StableKey → D2 重复行/已知部分重复（KNOWN PARTIAL REPEAT）两条解析路径均不适用 → 计数 +1 → `"Unknown interaction affordances remain; completeness cannot be proven."` | `Agent.OpenWorld.cs` L1243–1341；`/tmp/p26-fc-r1.json` terminal |

### 4.1 确定性否决：worker 的 "checkbox → Unknown?" 假设

`SettingsSemanticCapability.InterpretAsync` 按 occurrence 顺序评估 Pattern 1→7：该 checkbox occurrence（facts 组含
Text/ClassName/Geometry）在 Pattern 4 不中后，Pattern 5 `IsDuplicatePrimaryRowRendering` 命中（idx 2 menu_item
同文本 + bounds 重叠），`continue` 发出 NonInteractive 证据；Admission（`SemanticEvidenceV2Admission.Admit`）对该
envelope 无任何特异性拒绝分支（机械检查全部通过，源层 Primary ≤ MaximumPermittedTier）。
→ **idx 3 checkbox 的最终 affordance = NonInteractive（重复行），不是 Unknown。**
FAULT-CONTAINMENT §15/16 的 "checkbox → Unknown?" 因此**未获支持**；"可能未匹配 toggle pattern" 的机制担忧是
真实的（见 §5），但对**这个**元素不构成失败（被 Pattern 5 承接）。

## 5. Root Gap 分析：为什么 checkbox "永远无法成为 LocalControl"

三个独立断点（按管线第一次偏离排序）：

### FDP-1（适配器层，最先偏离）：`PhysicalEnvironment` 类型归一化被删除 — 契约回归

- 历史：`f054695`（PhysicalEnvironment 初版）含
  `private static string NormalizeType(...) => "switch" => "toggle", "checkbox" => "toggle", _ => rawType`，
  并在 per-candidate 处调用（注释 `// Normalize type: "switch" → "toggle" (adapter boundary)`）。
- 现状：commit **`e2d8dd4`**（DSH UniFlow 工具链 commit，非感知/Runtime 授权变更）删除了 `NormalizeType`
  及其调用点，改为 `var perceptionType = candidate.Type;`（L140 "Preserve provider output"）。
- 后果：`switch/checkbox` 以原始 provider 类型到达语义层；`IsLocalControl("checkbox")=false` 成为恒真，
  checkbox → LocalControl 在**任何**页面都不可达（除非 PrimitiveState 或 structured checkable 佐证）。
- 契约测试：`platforms/perception/tests/test_reality_repair.py::test_rper_06_canonical_switch_to_toggle_propagation`
  断言源码含 `"switch" => "toggle"` 与 `"checkbox" => "toggle"` — **实测 RED**（`pytest ... -k rper_06 → FAILED`）。
  该测试在 S1A 证据中被明确记录为"repair does not modify the Adapter"（契约锚点），删除未同步契约。
- Owner：`UniClaw.Runtime.Adapters`（`PhysicalEnvironment` 适配层 seam）↔ 感知契约（RPER-6）。
- GapKind：**CONTRACT_REGRESSION**（RED 测试 + 历史实现对照）。

### FDP-2（语义能力层）：capability 词汇不识别 vision `checkbox` provider type — 覆盖缺口

- `SettingsSemanticCapability.IsLocalControl`（L311–314）：仅认 `PrimitiveState != null` / `"toggle"` / `"switch"`；
  无 `"checkbox"`。`IsToggleShape`（L261–264）：仅认 `Checkable==true` / class 含 `"Switch"` / `"toggle"`；vision
  ClassName="checkbox" 不含 "Switch"，structured 根页无 checkable → 双双漏。
- 测试面：`ExternalSettingsSemanticCapabilityTests` 只覆盖 `toggle` provider（L161–202），**无 checkbox provider
  → LocalControl 的用例**。
- Owner：`UniClaw.Semantic.Settings`（SettingsSemanticCapability）。
- GapKind：**CAPABILITY_COVERAGE_GAP**（词汇"LocalControl"存在，但触发 pattern 漏掉 checkbox provider）。
  注意：若 FDP-1 恢复（checkbox→toggle 适配层归一化），本缺口在受控路径上不可达；两者是同一链条上的两层。

### FDP-3（验证组合层）：campaign 未接线视觉状态读取 — 自造成的诊断盲点

- `SettingsCampaignProgram` L113–118 构造 `PhysicalEnvironment` 时**未传** `visualControlFactory`（对比
  `PhysicalHostComposition.cs` L66–75 支持该 seam）→ 全部元素 `SwitchState=null` → 永不产生
  `SemanticObservationFactKind.BooleanState` → `IsLocalControl` 的 PrimitiveState 通道在 campaign 中恒关。
  换言之，**即便 checkbox 状态读取可用，该 campaign 也没有读取**。这是验证组合的遗漏，不属于产品能力。

### 4.2 实际阻塞点（同帧）

- idx 0 无文字 icon（搜索图标区，OCR 未给出文本，类型 input→icon 不稳定）→ 无 pattern 可分类 → Unknown →
  阻塞完整性。Owner：SettingsSemanticCapability（capability coverage，无"无文字图标 → NonInteractive"pattern）。
  GapKind：**CAPABILITY_COVERAGE_GAP**（即已登记的 ICON_TEXTLESS_PATTERN_GATE）。
- OCR 乱码不是根因：即便乱码，Pattern 5（结构重复行）成功承接；乱码只影响基于文本的稳定化/重复行语义匹配
  （若 menu_item 干净而 checkbox 乱码，Pattern 5 会同文本失败 → 该 checkbox 会 Unknown —— 本 run 中两副本同文本，
  未触发）。此风险记入残余项，不作为当前失败根因。

## 6. First Divergence Point / Owner / GapKind 汇总

| # | 项 | FDP | Owner | GapKind | 状态 |
|---|---|---|---|---|---|
| 1 | checkbox 无法成为 LocalControl（一般性） | `PhysicalEnvironment.cs` 适配层类型归一化（`e2d8dd4` 删除 `NormalizeType`） | UniClaw.Runtime.Adapters | CONTRACT_REGRESSION（RPER-6 RED） | CONFIRMED（活测试 + 源码历史） |
| 2 | 同上（语义层兜底） | `SettingsSemanticCapability.IsLocalControl/IsToggleShape` 缺 vision "checkbox" | UniClaw.Semantic.Settings | CAPABILITY_COVERAGE_GAP（无测试） | CONFIRMED（源码 + 测试面） |
| 3 | 同帧真实阻塞 | 无文字 icon → 无 pattern → Unknown → 完整性失败 | SettingsSemanticCapability | CAPABILITY_COVERAGE_GAP（=ICON_TEXTLESS_PATTERN_GATE） | CONFIRMED（源码 + frames + run 终态） |
| 4 | "checkbox → Unknown?"（FAULT-CONTAINMENT §15/16 假设） | —（假设本身被 Pattern 5 追踪否定；该元素实际 NonInteractive） | — | MISDIAGNOSED_HYPOTHESIS（未证实） | DISPROVEN（确定性追踪） |
| 5 | failing 帧 raw YOLO class/conf | — | — | — | INSUFFICIENT_EVIDENCE（未采集 stage views） |

## 7. INSUFFICIENT_EVIDENCE 注册表

1. failing 帧（seq 4/5）的**原始 YOLO class 名与 confidence**：campaign 未开 `capture_stage_views`，rawLabel 无落盘。
   若要闭合：在 evaluation L2 runner（`capture_stage_views=True`）或 campaign 侧开启 stage views 重拍同路径。
2. FC run 当时使用的 vision receipt（CURRENT ACTIVE vs `P26_VISION_RECEIPT` candidate）：run JSON 未记录 env。
3. FC run 的 per-element **admitted evidence**（`[semantic-diagnostic]` 走 stderr，未捕获）：本报告 §4 的分类结论
   是"源码逻辑 + 帧语料"的确定性追踪（证据等级 ~E2），不是 run 输出的直接观测。
4. seq 4/5 structured 层元素的 bounds（frames dump 只存 text/rid/cd/cls/clickable，无 bounds）：icon/佐证相关性
   只能凭文本不匹配 + class 推断，未做精确 bounds 验证（不影响本结论：根页 structured 无 checkable 节点）。

## 8. Human Gate 裁决项（交给 Leader）

按 `FAULT-CONTAINMENT-RESULT.md §22` 的 `CHECKBOX_GARBLED_TEXT_GATE` 语境，本诊断给出的裁决输入：

1. **CHECKBOX_GARBLED_TEXT_GATE 需要重新定性**：failing 元素并非 Unknown 阻塞（Pattern 5 已兜底 NonInteractive）；
   真正的下一层阻塞是**无文字 icon**（ICON_TEXTLESS_PATTERN_GATE）。若 Leader 仍希望 checkbox→LocalControl
   语义成立，须裁决 FDP-1/FDP-2 所有权：
   - 恢复适配器层 `NormalizeType`（checkbox/switch→toggle）并把 RPER-6 转绿（涉及 Adapter 契约；属于变更，需 gate）；
   - 或扩展 `SettingsSemanticCapability` 词汇（`checkbox` provider → IsLocalControl）+ 补测试（能力层新 pattern，需 gate）；
   - 或明确 checkbox 目标是"NonInteractive/重复行承接"（现状行为，零改动）——三选一的架构/契约裁定。
2. **FDP-3（验证组合）**：campaign 是否应接线 `visualControlFactory`（该 seam 已存在）以消除 Sw/checkbox 状态盲区。
3. 是否补采 failing 帧 stage views（闭合 §7.1 的 INSUFFICIENT_EVIDENCE）。

## 9. 边界声明

- 本 gate 零代码修改；未新增 Settings pattern；未修改 YOLO/感知管线；未做架构扩展。
- AuthorityDelta: NONE · ArchitectureDelta: NONE（纯诊断）。
- 结论全部挂源码 symbol / 活测试 / 帧语料 / run 产物；无法证明处显式标记 INSUFFICIENT_EVIDENCE。