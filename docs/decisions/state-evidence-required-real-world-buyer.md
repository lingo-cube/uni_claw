# StateEvidenceRequired Real-World Buyer — Gate Record

> Status: BUYER_ANALYSIS_COMPLETE（决策 G — 重观测策略 buyer，瞬态证据 gap）
> Date: 2026-08-17
> Prerequisites: REPAIR_L1_REAL_WORLD_SCENARIO_AND_RERUN = B（TUNING）· L1 架构本 gate 冻结
> Constraint: 零生产实现；L1 触发面/词汇/wire/bridge/consumer 不变；StateEvidenceRequired 不弱化

---

## 0. 既有感知工作对账（§0）

- `perception-actionable-toggle-evidence` + `-reality-repair` 已归档（GRADUATED）。
- reality-repair 毕业记录：ImageSwitchStateProvider 是 "sole switch-state authority"（130 行）；
  毕业记录明示 **"control recognition NOT claimed"（232 行）** 与 "Raw-pixel missing-class buyer
  demonstrated on Developer Options page"（219 行）——既有 change 覆盖"YOLO 无控制类时不虚构候选"
  （reality repair），**不覆盖**"控制候选存在但状态证据在动画窗口内读取为 null"。
- 关系：**C. 本观察的精确 buyer 未被既有 change 覆盖**（既有覆盖缺失类；本观察是存在类+瞬态状态读取）。

## 1–2. 真实 post-SetSwitch 链与物理生效证明（§1/§2）

```
Goal(WifiConnectivity.Enabled=true) → ObjectBinding(Wi‑Fi 行 + toggle)
  → SetSwitch → ADB tap 开关中心
  → 物理世界：ACTION_WORLD_EFFECT = A. EFFECT_CONFIRMED
      （真实 tap 开关中心 (969,618) 后 `cmd wifi status` → "Wifi is enabled"）
  → fresh 截图 → Vision → Observation → SwitchState → 状态证据 → StateEvidenceRequired
```

**物理动作确实生效**（独立 dumpsys 诊断，out-of-band 证据，不成为 Runtime truth）。

## 3. 原始感知证据（§3，真实帧）

| 证据（真实帧） | 结果 |
|---|---|
| toggle/switch 控制候选 | ✅ **存在**（`type=switch`，bounds x1:0.835 y1:0.299 x2:0.960 y2:0.345——页面顶部 Wi-Fi 开关） |
| 绑定锚 "Wi‑Fi" | ✅ 存在（menu_item） |
| bounds 有效性 | ✅ valid（[0,1]，x2>x1, y2>y1） |
| Python 原始层 switchState | 恒 null（Python 不产；真实填充在 C#） |
| **C# ImageSwitchStateProvider** | ✅ **工作**：OFF 帧 → `False`；ON 帧（物理 ENABLED 后）→ `True` |

## 4. 最早状态证据失败分类（§4）

**`C. CONTROL_PRESENT_STATE_FEATURE_MISSING`（瞬态）**，定位于：
- 物理生效 ✅（§2）；候选存在 ✅；bounds valid ✅；判定器工作 ✅（False/True 均正确）；
- **真实 run 的 SetSwitch 后 fresh 帧**落在开关**动画窗口**（material switch knob 移动中，
  ~200–300ms）——knob 位置分析对中间位置返回 null → SwitchState 未知 → `currentBelief is null`
  → StateEvidenceRequired。
- 与既有现场证据一致（`Agent.SemanticRun.cs` 注释：post-action 帧捕获到移动中 Wi-Fi 开关）。

## 5. SwitchState 生产路径审计（§5）

- 生产 producer：**`ImageSwitchStateProvider.ReadAsync`**（C# SkiaSharp，knob 位置分析）——**真实存在且工作**（本 gate 实测 False/True）。
- 触发条件：`PhysicalEnvironment.ObserveAsync` 中 `NormalizeType("switch")→"toggle"` + bounds valid → ReadAsync。
- **无"DTO 有字段但无 producer"问题**——producer 存在、被调用、正确；失败是**特定帧（动画窗口）返回 null**。

## 6. 能力 vs 机制（§6）

buyer 以能力表述：**TOGGLE_STATE_EVIDENCE（瞬态稳定化）**——控制候选存在、物理生效、
判定器可用；缺的是"动作后状态证据的有界稳定读取"（settle/重观测），**不是**新感知机制。

## 7–8. L1 相关性 / 重观测价值（§7/§8）

- **`L1_ASSISTANCE_EXPANSION_NOT_JUSTIFIED`**：外部建议无法制造缺失的世界证据；"re-observe" 建议
  仅当另一次观测能真实产出状态证据才有用——而能力已证明可产出（settle 后帧 = True）。
- **`TRANSIENT_EVIDENCE_GAP`**（非 STRUCTURAL）：稳定帧能读到正确状态（False/True 均验证）；
  真实失败帧在动画窗口。→ **settle/重观测策略是 buyer**（导航相位已有
  `NavigationTransitionSettle` 500ms×4 先例；SetSwitch 相位缺同类机制）。

## 9. buyer 矩阵（§9）

| 候选 | 证据 | 层 | 最早缺失 | 架构变更? | 既有 buyer? |
|---|---|---|---|---|---|
| A 动作效果验证修复 | 物理已生效 | 无 | 无 | 否 | 否 |
| B 原始控制候选生成修复 | 候选存在 | 无 | 无 | 否 | 否 |
| C toggle 状态感知修复 | 判定器工作（False/True） | 无 | 无 | 否 | 否 |
| D 融合状态传播修复 | 融合逻辑正确 | 无 | 无 | 否 | 否 |
| E 绑定状态传播修复 | 绑定正确 | 无 | 无 | 否 | 否 |
| F 状态信念归约修复 | reducer 正确 | 无 | 无 | 否 | 否 |
| G GoalEvidence 验证修复 | 验证正确（null→Required 是 truthful） | 无 | 无 | 否 | 否 |
| **H settle/重观测策略** | **动画窗口帧 → null（瞬态）；settle 后帧 → True** | Traversal/Agent 时序 | **SetSwitch 后有界 settle/重观测** | 否（时序策略） | 否 |
| I 既有 change 集成 | 能力已就绪未缺集成 | 无 | 无 | 否 | 否 |
| J 无 buyer/场景 | 场景已修复 | — | — | — | 否 |

## 10. 冻结边界（§10）

零改动：L1 触发面/词汇/wire/bridge/consumer/L2/L3/语义权威/GoalEvidence 要求；
**StateEvidenceRequired 不弱化**（状态无法证明时 fail-closed 保持正确）。

## 11. L1 操作验证状态（§11）

```
L1_ARCHITECTURE = SOUND
REAL_LLM_TRANSPORT = VERIFIED
REAL_L1_RECOVERY_CASE = NOT_YET_OBSERVED
L1_TUNING_IMPLEMENTATION = NOT_AUTHORIZED
```

零咨询 ≠ 模型失败（真实失败在 L1 触发面外，且本 buyer 与模型无关）。

---

## FINAL DECISION

**`G. REOBSERVATION_POLICY_BUYER_CONFIRMED`** — 真实 StateEvidenceRequired 根因 =
SetSwitch 后立即 fresh 帧落在开关**动画窗口**（knob 移动中），`ImageSwitchStateProvider`
对中间位置返回 null → 状态证据未知 → truthful fail-closed。能力链完整（物理生效
EFFECT_CONFIRMED、候选存在、bounds valid、判定器 False/True 均正确）；失败是
**TRANSIENT_EVIDENCE_GAP**（settle 后帧可正确读取）。buyer = **SetSwitch 相位的有界
settle/重观测策略**（导航相位 `NavigationTransitionSettle` 先例的对称扩展），非感知
能力、非 L1 扩张、非架构变更。既有 toggle change 不覆盖此 buyer（分类 C）。

---

## APPLY RESULT (2026-08-17) — POST_ACTION_STATE_SETTLE_READY_FOR_APPLY → IMPLEMENTED

**实现**：`openspec/changes/post-action-state-settle/`（design/spec/tasks）+ 
`src/UniClaw.Runtime/Traversal/Traversal.cs`（Verify-phase settle hook in 
`ExecuteLoweredActionAsync`：eligibility 谓词 + D. HYBRID settle loop +
`TraversalJournalEntry.PostActionSettleCount` 可观测重观测计数）。零 L1 变更。

**验证**：
- T1–T15 全通过（`PostActionStateSettleTests`：15/15，含 T13 dispatch-once 不变式）。
- 全量 .NET 套件 1246 pass / 11 fail（11 = 既有基线：5×VisionHostBehavioralProofs
  + 5×VisionIdentityVerificationTests + 1×Capstone real-emulator；REGRESSION_IMPACT
  = NONE_OBSERVED）。
- 真实模拟器 multilevel Wi-Fi 证明 **PROOF-MULTILEVEL PASS**（两次独立运行）：
  satisfied=True, exactlyOneSetSwitch=True, hops=2 eachHopFreshVerified=True,
  sourcePointsAtFresh=True, perceptionSwitchOn=True, postRunWifiOn=1。
  run#1 post-action seq=8（settle 介入：seq7 动画帧 null → seq8 稳定 True）；
  run#2 postActionSettleCount=0（立即有效证据 → 零多余 settle — T2/T15 真实行为）。

**结果**：`STATE_EVIDENCE_REQUIRED_TRANSIENT_FAILURE = ELIMINATED`；
`REAL_L0_WIFI_CLOSED_LOOP = COMPLETED`（正常状态转场 L0 本地闭合，无 L1 咨询）。
