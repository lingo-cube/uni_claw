# Design: physical-wifi-off-to-on-minimum-semantic-loop

## Context

Provider Foundation Reconciliation（`docs/decisions/provider-foundation-reconciliation.md`）结论：

- provider 层完整且隔离测试充分（Screenshot / ADB Dispatch / Vision 均为 ✅ 实现），但 **Integrated=❌**：全库无生产组合根
  （`src/` 无 `Program`/`Main`/DI；`PhysicalEnvironment` 仅在 `PhysicalEnvironmentCompositionTests.cs` 用 stub 构造，从未跑过 Agent）。
- 每次 Agent 运行的 `IEnvironment` 都是测试侧 Fake（`ScriptedEnvironment` / `ReplayEnvironment` / `SimulationEnvironment`）。
- 运行时核心（Runtime）对 Adapters 零 ProjectReference（Guard 1）；`IEnvironment` 消费点：`Startup`(Startup.cs:33)、`Traversal`(Traversal.cs:56)、`Recovery`(Recovery.cs:33)。
- 真实 IO deferred seam：`Traversal` sync-over-async（Traversal.cs:39-41，自带「Phase 4 接入真实 IO 时改为异步形状」裁决）、`Startup.AttachAsync` 空实现（Startup.cs:99-104，注释「真实 attach 由 Phase 4 Adapter 接入 — I-12」）。
- WiFi 物理机制 = 开关坐标处 ADB tap（DeviceActionTranslator.cs:61-74）；无 OS WiFi provider；OFF→ON 转换对为 SYNTHETIC（RealitySeededSettingsFixture.cs:138,:147,:158）。
- 状态验证链路：fresh Observation → perception → `ImageSwitchStateProvider`（确定性 luminance heuristic，无 ML）→ `GoalEvidence(..., observation.SequenceNumber)`（Agent.SemanticRun.cs:96）。`WorldBelief` 不携带场景语义字段 —— post-action 状态不进入 WorldState 副本，直接由 Observation evidence 判定（裁决 2）。

本 change：第一条真实 Agent Vertical Slice = 生产组合根 + emulator 现实校准 + WiFi OFF→ON 闭环。**只产出设计文档，不实施。**

## Goals / Non-Goals

**Goals:**
- 定义生产组合根的结构与位置（接线 `IEnvironment → PhysicalEnvironment → 真实 providers`）。
- 定义 Fake→Real 过渡的显式边界（选择权只在组合根）。
- 定义 emulator 现实校准方法与资产形态（替换 SYNTHETIC 标记）。
- 定义 OFF→ON 语义闭环的执行/终止/验证语义（复用既有确定性语义环，最小改动）。
- 定义 invariants、authority 边界、falsifiers、测试策略、runtime 证明要求。

**Non-Goals:**
- 不实施任何代码（本 change 仅 proposal/design/spec/tasks）。
- 不引入 provider registry / provider selection authority / 通用 workflow engine（设计文档明令禁止）。
- 不扩展 Runtime 语义 authority、不替换 Capability 模型、不改 perception/training 算法、不做 ReleasePolicy。
- 不接真实手机（宪章 §33，emulator-only）。

## Decisions

### D1: 组合根形态 —— 独立薄宿主项目，普通构造注入
新增宿主项目（如 `src/UniClaw.Runtime.PhysicalHost`，仅 `Main` + 构造 + 预检调用），引用 Runtime + Adapters；不做 DI 容器、不做 ServiceCollection。
- **备选（拒绝）**：在 `UniClaw.Runtime.Harness` 加入口 —— Harness 是 trace-capture 工具（ITraceCaptureStore），职责不混。
- **备选（拒绝）**：DI/registry —— 与「No Provider framework, registry, or plugin system」设计约束冲突（trace-capture-scenario-catalog-foundation/proposal.md:33、switch-state-reading/proposal.md:62）。
- **理由**：`PhysicalEnvironment` 已是构造注入风格（PhysicalEnvironment.cs:53-69）；组合根只做接线，天然满足「组合根无权决策」。

### D2: Traversal sync-over-async 落地（唯一核心改动）
按 Traversal.cs:39-41 自带裁决将 `ExecuteStep` 系列改为异步形状（真实 IO 需要 await）。
- **备选（拒绝）**：保留同步阻塞 —— 真实 adb/vision IO 会阻塞线程；该 seam 注释已明确「Phase 4 接入真实 IO 时改为异步形状」，本切片即 Phase 4 入口。
- **范围**：仅执行路径 async 化；Traversal 的确定性语义（Select→Check→Execute→Observe→Verify→Branch、journal、retry）与 authority 不变；Fake 场景测试因环境同步完成而行为等价。

### D3: Startup.AttachAsync 落地（emulator attach）
实现真实 attach：`AdbDevicePreflight`（含真实截图探针）→ serial 解析（`AdbDeviceResolver`，单设备确定性/多设备 fail-closed）→ Ready；失败 → `StartupResult.NotReady(原因)`，零动作分发。
- **备选（拒绝）**：保持 no-op —— 切片要求真实设备在启动时可用；预检门控是 fail-closed 前提。
- **理由**：`AdbDevicePreflight` 已含 4 轴 readiness（含截图探针），只需接线，不新增机制。

### D4: WiFi 物理机制 —— SetSwitch 保持 tap-at-coordinates，不引入 OS provider
物理机制保持「开关坐标处 ADB tap」（DeviceActionTranslator.cs:61-74）；`SemanticGoalInput("WifiConnectivity","Enabled",true)` 走 UI 语义环达成。
- **备选（拒绝）**：`adb shell svc wifi enable` 直接切换 —— 绕过 UI 世界，破坏切片目的（证明 Agent 闭环），且违反「世界状态只能经 Observe 确认」；同时 spec 明令禁止 OS WiFi provider。
- **理由**：幂等期望语义（已满足→NoOp、未知→不分发）由 lowerer 保持（SemanticActionLowerer.cs:78-83）；tap 后必须 fresh Observation 验证。

### D5: 目标环境 —— emulator-5554（与既有资产一致）
校准与闭环运行以 emulator-5554 为目标（既有 committed assets 来自 emulator-5554：`LiveCalibrationTests.cs:12-25`）。
- **备选（拒绝）**：真实手机 —— 宪章 §33「第一阶段不要连接真实手机」；本 change 附带 §33 门决策记录（emulator-only）。
- **理由**：可重复、无硬件依赖、与既有录制现实资产同源。

### D6: 校准资产形态 —— 录制现实对 + provenance
首次运行组合根录制真实 OFF→ON 转换对（screencap + 感知证据 + 序列），存 `tests/UniClaw.Runtime.Tests/Perception/Assets/`（或同源新目录），附 provenance（时间/设备/序列/来源说明），替换 SYNTHETIC 标记。
- **备选（拒绝）**：手工标注 —— 无记录在案的真实 OFF→ON 对（RealitySeededWifiScenarioTests.cs:12 明示「no recorded pair exists」），必须新鲜录制。
- **理由**：`RealImageClassifierTests` / `LiveCalibrationTests` 已有「记录现实资产」先例（RECORDED_REALITY 标注）。

### D7: 验证 authority 不变 —— Agent 语义环验证
post-action 状态验证仍由 Agent 语义环执行（Agent.SemanticRun.cs:96：`GoalEvidence(true, stateKey 满足, observation.SequenceNumber)`）；组合根/Provider 永不判定 Goal；dispatch 收据 ≠ 世界效果证据（裁决 10）。
- **备选（拒绝）**：在 Adapters 层做完成判定 —— 违反「一个 decision 只有一个 authority」（宪章 §29-31）。

## Implementation Slices（PROJECT_LEADER_IMPLEMENTATION_AUTHORIZATION_DECISION 批准的执行拆分）

### Slice 1 — REALITY_COMPOSITION_FOUNDATION
范围：生产组合根 + Host 项目/运行时接线 + 真实 IEnvironment 构造路径 + Fake/Replay/Simulation 保持测试侧 + Startup.AttachAsync 落地 + async seam 修复。
证明：**Agent 可以以真实环境依赖图运行**（组合根启动 → Ready → 一次 ObserveAsync 新鲜观测 → Agent 建立初始 belief；不含 WiFi 行为）。
禁止：WiFi 行为实现、provider registry、provider discovery、capability redesign。

### Slice 2 — WIFI_SEMANTIC_LOOP
范围：WiFi capability 执行 + ADB-backed action path + 动作后 fresh screenshot + perception 验证 + GoalEvidence 闭合。
证明：Goal WiFi OFF→ON；**唯一成功条件** = Action receipt + Fresh Observation + Perception Evidence + GoalEvidence（dispatch receipt ≠ world state change）。
开始条件：Slice 1 证明（tasks 4.1/4.2）+ Tier 0 回归（3.3）全绿后进入。

## Implementation Constraints（Gate 强制）

1. 无 `svc wifi` / `cmd wifi`（spec 禁止 OS WiFi provider）。
2. 无隐藏 emulator API（emulator console wifi 命令、UiAutomator 隐藏接口）。
3. 无直接 WorldState/WorldBelief 改写 —— 世界状态仅经 fresh Observation evidence 进入判定（裁决 2）。
4. 无场景特定状态注入生产路径 —— 校准资产/RealitySeeded fixture 仅测试侧。
5. 物理效果仅经 UI 语义环达成（tap → screencap → perception → 验证）。

## Authority Boundary Audit（Gate 复核，代码证据）

| 层 | 边界 | 代码证据 |
|------|------|----------|
| **Agent** | 拥有 decision（capability 选择、授权、终止判定）与 verification（GoalEvidence 闭合） | `SelectCapability`（Agent.SemanticRun.cs:111-117,:185-191）、authorize（:120-125）、`GoalEvidence(..., observation.SequenceNumber)`（:96）、`CompleteSemantic`（:201-204） |
| **Capability** | 拥有 intent-level action 的定义维度（声明式匹配，不执行） | `Capability(Name, ApplicableToCategory, StateDimension)`（Model/Capability.cs:19）；intent 实例（SemanticAction）由 Agent 依 capability 产生（Agent.SemanticRun.cs:120-121），lowering 由无状态 `SemanticActionLowerer`（SemanticActionLowerer.cs:78-83）完成 —— 本切片不替换该模型 |
| **Provider（Operator）** | 只拥有机制执行；成功永不等于世界效果证据 | `AdbDispatchTarget`「a success is never world-effect evidence」（Operator/AdbDispatchTarget.cs:6-7）、无状态 `DeviceActionTranslator`（Operator/DeviceActionTranslator.cs:9-15） |
| **Perception** | 只拥有 evidence（候选、switch-state 读取、fail-closed 诊断） | `LocalVisionPerceptionSource` → `PerceptionCandidate`；`ImageSwitchStateProvider`（确定性 luminance heuristic，无 ML）；`SwitchStateValidation` 陈旧帧 fail-closed |
| **Environment** | 只拥有 observation/action transport | `IEnvironment`「端口只回答看到什么/请执行这个动作」（Environment/IEnvironment.cs:8-11）；`PhysicalEnvironment`「Owns external integration mechanics ONLY」无语义信念（Adapters/PhysicalEnvironment.cs:20-22） |

审计结论：五层边界与既有代码一致，本切片不改变任何一层 authority。

## Invariants（本切片强制保持）

- I-1/Guard 1：Runtime 核心零 ProjectReference（组合根在宿主项目）。
- I-4：Observation 是 evidence 不是 truth；Fingerprint 是 evidence 不是 identity。
- I-10：Completion 必须由 Goal Evidence 证明（fresh post-dispatch Observation）。
- 裁决 10：dispatch 结果（Dispatched/TimedOut/Rejected）≠ 世界效果证据。
- 裁决 2：post-action 状态不复制进 WorldBelief/WorldState 副本，直接由 Observation evidence 判定。
- 裁决 7：单一 Runtime slice，不建独立 Runner/framework/registry。
- 宪章 §33：emulator-only，不接真实手机（本 change 附决策记录）。

## Authority Boundaries

| 层 | 拥有 | 不拥有 |
|------|------|--------|
| 组合根（新宿主项目） | 构造、接线、预检调用、进程生命周期 | 任何语义决策：capability 选择、Goal 判定、recovery 决策、provider 选择 |
| Agent | Goal/World Belief/capability 选择/授权/完成判定（SATISFIED 唯一发出者） | 元素匹配、点击实现、OCR、环境接线 |
| Traversal | 单步执行内核（Select→Check→Execute→Observe→Verify→Branch）、journal | 世界级语义理解、Goal 判定 |
| Environment (`PhysicalEnvironment`) | 观测与动作分发的世界端口（screenshot→perception→vision→Observation；translate→dispatch） | 任务决策、语义信念 |
| Adapters provider（Screenshot/ADB/Vision） | 机制执行（screencap/tap/dispatch/switch-state 读取）与 fail-closed 语义 | capability 选择、世界效果证明（收据仅机制结果） |

## Falsifiers（本切片必须能被证伪）

1. **组合根越权**：组合根代码出现 capability 选择、Goal 判定或 recovery 决策 → 违反「组合根无权决策」。
2. **registry 复活**：出现 provider registry / ServiceCollection / provider 选择表 → 违反设计约束与 spec。
3. **dispatch 当证据**：任何路径以 Dispatched 收据直接判定 SATISFIED（无 fresh Observation 验证）→ 违反 I-10/裁决 10。
4. **幂等破坏**：世界已满足仍分发物理动作（lowerer NoOp 被绕过）→ 违反 SetSwitch 期望语义。
5. **未知状态盲动**：状态 Unknown 时仍分发 → 违反 STATE_EVIDENCE_REQUIRED 语义。
6. **OS provider 引入**：出现 `svc wifi`/`cmd wifi`/WifiController 类 → 违反 spec 边界。
7. **Fake 套件回归**：确定性场景套件（SC-P1-001..005 及 frozen 13 capability）失败或 Fake 环境被改动 → 违反过渡边界。
8. **Guard 1 破坏**：Runtime 项目出现对 Adapters 的 ProjectReference → 构建 Guard 失败。
9. **无 trace 因果链**：闭环运行无法重建 Goal→capability→token→dispatch→observation→verification 因果链 → 违反 runtime 证明要求。

### Gate-required falsifiers（PROJECT_LEADER_IMPLEMENTATION_AUTHORIZATION_DECISION 门槛）

| # | 证伪点 | 强制方式 | 对应任务 |
|---|--------|----------|----------|
| F1 | Fake 环境意外进入生产路径 | 架构断言：Runtime 核心零 Adapters 引用（Guard 1）；宿主项目是 `PhysicalEnvironment` 唯一生产构造点；Runtime 无 Fake-vs-Physical flag/选择分支 | 2.6 |
| F2 | 无真实设备仍分发 | 预检门控：未 Ready 前 Traversal 不得执行；预检失败 → NotReady + ActionHistory 为空（双入口断言） | 4.2 |
| F3 | dispatch 成功但无观测即满足 Goal | I-10/裁决 10：SATISFIED 仅可由 fresh Observation evidence 产生；「Dispatched 但世界未变」测试必须以非 SATISFIED 终止 | 6.2 |
| F4 | 陈旧截图验证成功 | 序列推进强制（Traversal.cs:245 未推进即 fail step）+ `SwitchStateValidation` 陈旧帧 fail-closed；pre-dispatch 截图断言 SATISFIED 的测试必须失败 | 6.3 |
| F5 | 失败动作误触发恢复 | 单次失败 → `TraversalStepResult.Failed` → 经 Container 转交 Agent（SC-P1-004 escalate 不偷权）；恢复仅按 Agent scope 进入（Recovery 入口断言） | 6.4 |
| F6 | provider 失败产生语义成功 | perception/dispatch 失败 fail-closed → Unknown → STATE_EVIDENCE_REQUIRED 或 step Failed；任何路径不得 SATISFIED | 6.5 |

## Test Strategy

分层（真实 tier 与确定性 Fake tier 严格分离）：

1. **Tier 0 — 既有确定性套件（回归）**：Fake 场景 + Guard + Perception 全绿；证明 D2 async 化未改变语义（Fake 环境同步完成 → 行为等价）。
2. **Tier 1 — 组合根单元**：宿主组合根在注入替身（fake runner/source）下可构造；预检失败 → NotReady、零动作（无 emulator 依赖，可入普通套件）。
3. **Tier 2 — 真实集成（emulator 门控，独立 tier）**：`AdbDevicePreflight` 通过 → 组合根真实启动 → OFF→ON 闭环 SATISFIED（fresh observation 证据）；幂等/Unknown/未变 falsifier 变体。
4. **Tier 3 — 校准资产测试**：`ImageSwitchStateProvider` 对 OFF/ON 资产确定性判定；SYNTHETIC 标记替换后 provenance 断言。
5. **Tier 4 — trace 证明**：闭环运行 trace 可重建每跳因果链（runtime-observability-trace-foundation 契约）。

CI 约束：Tier 2 需要 emulator 前置（与 CORR_HOST03/04 同样以长超时 + 显式失败处理，不静默 Skip）；Tier 0/1/3 无环境依赖。

## Runtime Proof Requirements

切片证明 = 一次真实 OFF→ON 运行满足：

- 终止 `SemanticRunResult.Satisfied` 且 `GoalEvidence.Satisfied == true`；
- `GoalEvidence.SourceObservationSequence` > dispatch 前最后一次观测序列号（fresh evidence）；
- trace 中每跳可重建（Goal → capability → SetSwitch token → dispatch 收据 → post-dispatch observation → verification）；
- 至少一条 falsifier 变体（dispatch 成功但世界未变）以非 SATISFIED 终止。

## Risks / Trade-offs

- **[emulator 行为抖动]**（截图时序/动画/渲染延迟）→ 预检截图探针 + 既有 Traversal retry 语义（Traversal.cs:118-261）+ 超时预算；Tier 2 独立于普通套件，不污染确定性回归。
- **[luminance heuristic 在 live frame 上误判]**（无 ML 的确定性启发式）→ Tier 3 校准资产 + fail-closed Unknown（未知即 STATE_EVIDENCE_REQUIRED，不盲动）。
- **[宪章 §33 门冲突]**（Phase 1 不接真实设备）→ 本 change 为 Phase 4 入口（设计明确标注「Phase 4 接入真实 IO」）；附 §33 门决策记录：emulator-only、满足 §57 第一阶段标准后进入。
- **[async 化回归风险]** → Tier 0 全量回归 + 语义 authority 不变原则（只改 IO 形状，不改决策）。
- **[测试环境依赖扩散]** → Tier 2 显式门控，不进普通套件；与 Vision Host 测试同样的显式失败策略（长超时，不静默 Skip）。

## Migration Plan

1. 本 change 归档后（另行授权）按 tasks.md 顺序实施：宿主项目 → 核心 seam → 校准 → 闭环 → 验证。
2. 回滚策略：宿主项目与 Tier 2 独立；核心改动仅 Traversal async 化与 Startup attach，可单独 revert 而不影响 Fake 套件（Tier 0 保持绿）。

## Open Questions

1. 宿主项目命名：`UniClaw.Runtime.PhysicalHost` vs `UniClaw.Runtime.Host.Physical`（实施时定，不影响本设计）。
2. async 化是否触及 Recovery 路径（Recovery.cs:71/83 同样消费 IEnvironment）—— 设计默认仅 Traversal 执行路径，实施时按 Guard/契约复核。
3. CI 是否提供 emulator runner（Tier 2 运行位置）—— 若 CI 无 emulator，Tier 2 降级为本地验证 + 显式失败说明（不静默 Skip）。
