# PROJECT_LEADER_PHYSICAL_WIFI_SLICE2_GRADUATION_DECISION

- **Authority**: `PROJECT_LEADER_PHYSICAL_WIFI_SLICE2_GRADUATION_REVIEW`
- **Date**: 2026-08-14
- **Input**: `IMPLEMENTATION_RESULT_ONLY — Slice 2`（WIFI_SEMANTIC_LOOP）+ 仓库核对 + 现场证明重放
- **Mode**: Graduation review only. No implementation performed.
- **Predecessor**: `APPROVED_SLICE2`（`docs/decisions/physical-wifi-slice1-graduation-decision.md`）

---

## 决策：**GRADUATED_PHYSICAL_WIFI_MINIMUM_SEMANTIC_LOOP**

第一条真实 Agent → Reality → Fresh Evidence → GoalEvidence 语义闭环成立。
成功链全部边为 **REAL**（emulator 现实），唯一完成 authority 为
fresh post-dispatch Observation + Perception 证据 + GoalEvidence + Agent 裁决。

---

## 1. End-to-End Authority Chain — **REAL**（全部边）

| 边 | 分类 | 证据 |
|---|---|---|
| SemanticGoal `WifiConnectivity.Enabled=true` | REAL | Program.cs `RunSlice2ProofAsync` Step 3/5（调用侧声明） |
| Agent 读取 Container belief | REAL | `Agent.SemanticRun.cs:93` `container.ObjectStateBeliefs`（belief 仅来自 Reconcile.FromObservation，无宿主注入） |
| Capability 选择 SetEnabled | REAL | `SelectCapability`（:112-116）；live trace `semantic capability selected: SetEnabled` |
| Agent 授权 SemanticAction | REAL | `AuthorizeAction`（:122-125） |
| Traversal lowering | REAL | `RuntimeTraversal.LowerAction`（:134） |
| PhysicalEnvironment | REAL | `PhysicalEnvironment.ExecuteAsync`（translate→dispatch） |
| DeviceActionTranslator | REAL | `SetSwitch → Tap(bounds 中心)`（机制级，无语义） |
| AdbDispatchTarget | REAL | `input tap x y`（ArgumentList tokens，无 shell 插值） |
| physical tap | REAL | live emulator-5554 实际执行 |
| fresh screenshot | REAL | `ObserveAsync` → AdbScreenshotSource 新截图（seq 2→3 推进） |
| Perception evidence | REAL | LocalVisionPerceptionSource → vision server（YOLO switch + OCR Wi‑Fi 行对齐）；live candidate_8 switch bounds 与录制一致 |
| Container belief update | REAL | `RefreshSemanticSnapshot` + `StateBeliefReducer`（观测局部、binding 限定） |
| fresh GoalEvidence | REAL | `GoalEvidence(SourceObservationSequence=3)` — live |
| Agent completion | REAL | `Satisfied`，exit 0 — live |

**成功链自身为 REAL**。无 TEST_ONLY / SYNTHETIC 边参与成功判定。

## 2. Success Authority — **PASS**

- `ADB exit zero ≠ world success`：AdbDispatchTarget 返回 `Dispatched` 且注明「world effect is unverified」；Rejected/TimedOut 独立映射。
- `TraversalStepResult.Succeeded ≠ Goal success`：S2F3 现场证明 — 2 次 Succeeded dispatch（fresh 序列推进）但世界未变 → `BudgetExhausted`，**绝不 SATISFIED**。
- `settings get global wifi_on = 1 ≠ GoalEvidence authority`：wifi_on 读回仅作 Step 7 佐证打印（「非成功条件，仅佐证」）；`proofSlice2` 断言不含 wifi_on（= satisfied ∧ exactlyOneSetSwitch ∧ freshObservationAdvanced ∧ sourcePointsAtFresh ∧ perceptionSwitchOn）。
- **唯一完成 authority**：fresh post-dispatch Observation + Perception 证据 + GoalEvidence + Agent 裁决。
- `GoalEvidence.SourceObservationSequence (3) == fresh 观测 (seq 3)`，且该观测 toggle 元素 `SwitchState=True`（live `sourcePointsAtFresh=True`, `perceptionSwitchOn=True`）。陈旧/pre-action 观测无法完成 Goal：Traversal `fresh.SequenceNumber <= observation.SequenceNumber → Failed`（F4）+ SwitchStateValidation 陈旧帧 fail-closed（F4b）+ 环结构（belief 判定仅用当前 `observation`）。

## 3. Frame Identity Fix Review — **PASS**

- 修复正确且为**通用所有权修复**，非 WiFi 专用 patch：`PhysicalEnvironment.ObserveAsync` 现从 `ImageSwitchStateProvider.Frame` 派生观测帧（reader 拥有该 capture 的 PerceptionFrame），不再构造第二个实例。
- 一张截图 → 一个帧身份；该帧派生的一切证据绑定同一身份；Observation 携带同一帧。
- 陈旧/异帧证据 fail-closed（`SwitchStateValidation`）。
- 无 provider 特定例外：修复前反模式仅存在于 `PhysicalEnvironment.ObserveAsync` 一处（生产唯一 `new PerceptionFrame()` 现只剩 ImageSwitchStateProvider 构造器）；PhysicalEnvironment 仅一条 ObserveAsync 路径、唯一 ISwitchStateReader 消费者。**无其他感知路径残留同类反模式，无后续压力项。**

## 4. WiFi Specialization Audit — **PASS**

- Adapters：`TurnWifiOn / WifiProvider / WifiSuccess / WifiCoordinates / WifiGoalCompleted / WifiConnectivity / SetEnabled` 全部 **0 命中**；AdbDispatchTarget 仅注释明示「provider 永不解读 WiFi 语义/目标/成功」。
- Core Runtime：11 处 "wifi" 全为文档注释示例（WorldBelief「不复制场景特定语义字段（如 WiFi 开关状态）」、SemanticAction/GoalInput 泛化示例等），零功能知识。
- 允许形态：`WifiConnectivity.Enabled` = 调用侧声明的场景语义对象；`SetEnabled / SetSwitch` = 既有泛化语义模式。
- Provider 只知 Tap / 坐标 / 机制（`DeviceActionTranslator` 注释：「DesiredValue 语义由 Runtime 处理，Operator 只需 tap 开关」）。

## 5. Host-Side Declared Knowledge — **PASS**

注入清单（Program.cs Step 3）：语义对象定义、状态维度、ElementBindingCriteria（"Wi-Fi" 文本锚 + "toggle" 控件类型）、PageAnalysisCriteria、启动 intent。全部为声明式领域知识（裁决 11）。
**未注入**：当前 WiFi 真值、动作后成功、场景假状态、隐藏 ON/OFF、绕过观测的固定答案。
**现场证明当前真值来自感知**：live 初始观测（seq 2）读到 OFF（belief=false）才触发 dispatch — 若为 UNKNOWN 则 `StateEvidenceRequired` 零分发、若为 ON 则幂等零分发；`exactlyOneSetSwitch=True` 只能由「感知读到 OFF」产生。

## 6. Calibration Leakage Audit — **PASS**

- 录制资产（帧 / perception JSON / provenance.json）仅存在于 `tests/.../Perception/Assets/wifi-slice2-calibration/`（测试侧 fixture）。
- src/ 对资产的全部引用为 Program.cs **文档注释**（:135/:283/:287）+ 设备准备常量 `RecordedSwitchCenterPx`（**仅**用于 Agent 运行外的 OFF 基线准备，不进语义路径）。
- 生产无 fixture JSON 加载、无合成转场、无硬编码 ON/OFF 结果（分类器计算得出）、无 emulator 特定语义状态注入。
- 测试资产未成为运行时真值。

## 7. Switch Classifier Review — **PASS**

- 通用视觉机制：亮度离群 vs 带中位数（轨道为主体的中位基线），主题无关（深/浅 knob、灰/青轨道），无 WiFi 标签依赖。
- fail-closed：无不对称离群质量 → null（UNKNOWN）。
- OFF=false / ON=true 由左/右离群比例差（±0.15）判定。
- 同帧要求保留：PhysicalEnvironment 仅对 toggle 候选 + 有效 bounds 调用 `ReadAsync`，结果经 `ValidateFrameMatch` 绑定观测帧。
- bounds 必须属于观测候选：`candidate.Bounds` 传入（感知输出），无候选 → 无读取。
- **无候选/无 binding 时不可能产生状态断言**：`StateBeliefReducer` 要求元素 ∈ binding.ElementIndices 且 PerceptionType=="toggle" 且 SwitchState 非空；否则 belief=null → `StateEvidenceRequired` 零分发。
- 合成主题测试仅测试侧；live 证据为真实 emulator 帧（`postActionSwitchState=True`）。

## 8. Action Count / Idempotence — **PASS**

- Live OFF→ON：`exactlyOneSetSwitch=True`（恰 1 次 SetSwitch）；`launchAppDispatches=1`（Startup bootstrap，Slice 1 不变式）。
- Already ON：S2E6 — `Satisfied`、**零 SetSwitch**、零分发（决策-only，fresh 证据）。
- 无隐藏前置动作制造 Agent 事后声称的转场：设备准备在语义 run 之外；准备用直接 adb（`RunAdbSilentAsync`），**不经** `environment.ExecuteAsync` → 不进入 ActionHistory（live `setSwitchDispatches=1` 即全部 Agent 分发）。
- 准备制造的是 OFF **基线**（起点），Agent 声称并实际完成的转场是 OFF→ON（由其自身 SetSwitch 创建）。

## 9. Baseline Preparation Review — **PASS**

- 准备（am start 落 Internet 页 + 录制中心 tap）发生在 `RunSemanticGoalAsync` 之前，属 TEST/PROOF FIXTURE SETUP；不计为 Agent capability 执行；不注入语义真值。
- 现场因果链成立：准备后 `baselineWifiOnAfterPrep=0` → Agent 初始观测（seq2）独立读到 OFF（belief=false，否则零分发）→ Agent 选择 SetEnabled → 物理效果（tap）→ fresh 观测（seq3）toggle=True → SATISFIED → 独立读回 `postRunWifiOn=1`。
- Agent 不假设准备产物：UNKNOWN/ON 两种分支均有零分发行为（F6a / S2E6）。

## 10. Multi-Level / Navigation Scope — **A. 单页语义动作环**

证明起于 `android.settings.WIFI_SETTINGS` 启动意图直落 Internet 页（WifiSettingsActivity），语义闭环全部发生在该单页（Wi‑Fi 开关行）。**不声称**多页/多容器自主遍历。
**MultiLevelTraversalClaim: NOT_PROVEN_AND_NOT_CLAIMED** — 记为未来场景压力（页面内导航/多容器需独立切片）。

## 11. Falsifier Replay — 全部通过（fresh 重放 40/40 含 8 条 Slice 2 测试）

| Falsifier | 证据 | 结果 |
|---|---|---|
| F2 无设备 | Live `--serial dead-serial-999`：`PROOF-F2 deviceUnavailable=true zeroDispatch=true zeroTraversal=true`，exit 2 | ✓ 零分发/零遍历 |
| F3 dispatch 成功但世界未变 | S2F3：2× Succeeded + fresh 序列推进，perception 恒 OFF → `BudgetExhausted`，非 SATISFIED | ✓ 收据≠世界变化 |
| F4 陈旧观测 | S2F4（post-obs 序列未推进 → `ExecutionFailed`）；S2F4b（SwitchStateValidation 异帧→null）；E2E 期间发现的帧身份不一致即 F4 fail-closed 的现场表现 | ✓ fail-closed |
| F5 Rejected 动作 | S2F5：`ExecutionFailed("Semantic action rejected")`，RecoveryProbe 未进入（恢复仅 Agent scope 决策），非 SATISFIED | ✓ 无编造恢复成功 |
| F6 perception/provider 失败 | S2F6a/b：`StateEvidenceRequired` 零分发；Live 死 vision socket → `BindingUnresolved` 零 SetSwitch，exit 2（`PROOF-F6 perceptionFailureNoSemanticSuccess=True`） | ✓ 无语义成功 |
| 幂等 already ON | S2E6：`Satisfied`、零 SetSwitch、零分发（决策-only，fresh 证据） | ✓ |

## 12. Forbidden-Mechanism Sweep — **PASS**

- src/（含 PhysicalHost）：`svc wifi` / `cmd wifi` / `emulator console` / `UiAutomator` / `settings put` / `wifi_on =` 写路径 / WorldState = **全部 0**（6.7 约束测试 5/5 + 本次复查双证）。
- `settings get global wifi_on` 为 **READBACK ONLY**：ArgumentList token 构建、无 shell 插值；仅用于基线判定与运行后佐证打印，**非执行/语义验证依赖**（proofSlice2 不含它）。

## 13. Runtime / Authority Deltas

- **RuntimeDelta: NONE**（Slice 2 本体未触碰 src/UniClaw.Runtime；语义环/遍历/Agent/Container 原样。工作树中的 async seam 属 Slice 1 已评审跨度，仅执行形状 sync→async，语义不变）
- **SemanticDelta: NONE**
- **AuthorityDelta: NONE**
- 职责不变：Agent=决策+完成 authority；Container=mutable 局部信念 owner；Traversal=执行/验证协议 owner；Environment=外部传输；Perception=证据生产者；ADB=纯机制。

## 14. Reality Level

- 前最强链：REPLAY
- Slice 1 后：REAL_ENVIRONMENT_OBSERVATION
- **Slice 2 后：EMULATOR_REALITY_END_TO_END_SEMANTIC_LOOP**
- 不称 REAL_DEVICE_PROVEN（§33 emulator-only）。

## 15. OpenSpec / Archive

- `openspec validate --changes`：本 change **PASS**（16/17 通过；唯一失败 `trace-capture-scenario-catalog-foundation` 为既有无关 Phase 1 change）。
- `openspec status`：4 项 artifact 全 done，isComplete。
- tasks.md：6.1–6.8、7.1–7.5 全部 `[x]`。
- 已归档：`openspec/changes/archive/physical-wifi-off-to-on-minimum-semantic-loop/`（仓库惯例：无日期前缀直移，同 `switch-state-reading` 先例）。
- 决策同步：本文档（docs/decisions/ 惯例）；四层文档无变动项（无新 enum/dispatch-table/layer；check-consistency.sh ALL PASS）。

## 回归

**940/940 通过、0 失败**（fresh：Tier 0/1/3 + 校准 12 + falsifier 8 + 约束 5 + 其余全量）；build 0 错误、0 新增警告（11 条既有基线警告未触碰）。Slice 2 falsifier/校准/约束/StaleFrameSafety/ModelImmutability 子集重放 40/40。

## 未来场景压力（记录，不阻断）

1. 多页/多容器自主遍历（本切片为单页环）。
2. 页面内导航能力（开关行外元素）独立切片。
3. 真实设备（§33 之外）验证。
