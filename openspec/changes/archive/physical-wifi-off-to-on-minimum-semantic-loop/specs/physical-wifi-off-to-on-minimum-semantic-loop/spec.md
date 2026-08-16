# Spec: physical-wifi-off-to-on-minimum-semantic-loop

> 第一条真实 Agent Vertical Slice：生产组合根 + 真实 provider 接线 + emulator 现实校准 + WiFi OFF→ON 语义闭环。
> 本规格定义 WHAT（SHALL）；HOW 见 design.md；实施步骤见 tasks.md。
> 权威约束：宪章 §33（emulator-only，不接真实手机）、I-4（Observation 是 evidence 不是 truth）、
> I-10（Completion 必须由 Goal Evidence 证明）、裁决 10（dispatch 结果 ≠ 世界效果证据）、
> 裁决 7（单一 Runtime slice，不建独立 Runner/registry）。

## ADDED Requirements

### Requirement: 生产组合根接线真实 IEnvironment

系统 SHALL 存在一个生产组合根（宿主项目入口），通过普通构造注入组合
`AdbDeviceResolver/Preflight → PhysicalEnvironment(AdbScreenshotSource, LocalVisionPerceptionSource, ImageSwitchStateProvider, AdbDispatchTarget) → Startup/Traversal/Recovery/Agent`，
并将该 `PhysicalEnvironment` 作为唯一 `IEnvironment` 注入运行内核（Startup.cs:33 / Traversal.cs:56 / Recovery.cs:33）。

#### Scenario: 组合根启动注入真实环境

- **WHEN** 组合根在 emulator 已连接且 adb 可用的前置下启动
- **THEN** Startup/Traversal/Recovery 收到的 `IEnvironment` 是 `PhysicalEnvironment` 实例，其 `ObserveAsync` 返回由真实 `adb screencap` 派生、经 perception/vision 富化且序列号单调递增的 Observation

#### Scenario: Runtime 核心零引用保持

- **WHEN** 构建 `src/UniClaw.Runtime` 项目
- **THEN** 该项目对 `UniClaw.Runtime.Adapters` 的 ProjectReference 数量为 0（Guard 1 保持），组合根存在于独立宿主项目

### Requirement: 设备预检门控启动

系统 SHALL 在 Ready 之前执行 `AdbDevicePreflight`（含真实截图探针）；预检失败时启动 SHALL 以 `StartupResult.NotReady(显式原因)` 终止，且 SHALL NOT 分发任何设备动作。

#### Scenario: 预检失败即 NotReady

- **WHEN** emulator 未连接、serial 无法解析或截图探针失败
- **THEN** 启动结果为 NotReady 并携带显式原因，且 ActionHistory 为空（零动作分发）

#### Scenario: 预检通过才 Ready

- **WHEN** 预检四项全部通过（adb 可用 / 设备可达 / serial 唯一 / 截图探针成功）
- **THEN** 启动结果为 Ready 并建立 RecoveryAnchor

### Requirement: Fake→Real 过渡显式且仅存在于组合根

系统 SHALL 保持测试侧 Fake 环境（`ScriptedEnvironment` / `ReplayEnvironment` / `SimulationEnvironment`）原样且全绿；真实与虚假环境的选择 SHALL 只发生在组合根，Runtime 核心内 SHALL NOT 出现 Fake-vs-Physical 的 switch、flag 或环境选择逻辑。

#### Scenario: 确定性 Fake 套件不回归

- **WHEN** 实施本 change 后运行既有确定性场景套件（SC-P1-001..005 及其后 13 个 frozen capability 场景）
- **THEN** 全部通过，且 Fake 环境的构造与行为未被修改

#### Scenario: Runtime 核心无环境选择逻辑

- **WHEN** 检索 `src/UniClaw.Runtime` 内对 Adapters/PhysicalEnvironment 的引用或环境选择分支
- **THEN** 结果为空（组合根是唯一接线点）

### Requirement: WiFi OFF→ON 语义闭环以 Goal Evidence 终止

系统 SHALL 对 `SemanticGoalInput("WifiConnectivity","Enabled",true)` 执行完整链：
UserGoal → SemanticGoal → Agent Decision（READ→DECIDE）→ Capability Selection（`ApplicableToCategory`+`StateDimension` 恰好一个匹配）→ Action Token（SetSwitch true）→ Provider Dispatch（tap at 开关坐标）→ Physical Effect → Fresh Observation（post-dispatch 序列号推进）→ Perception Evidence → State Verification。
仅当新鲜 post-dispatch Observation 的感知证据显示 Enabled=true 时，系统 SHALL 以 `SemanticRunResult.Satisfied` 终止，且 `GoalEvidence.SourceObservationSequence` SHALL 指向该新鲜观测；dispatch 收据本身 SHALL NOT 构成完成证据。

#### Scenario: Happy path 达成 SATISFIED

- **WHEN** 世界为 WiFi OFF，运行 `SemanticGoalInput("WifiConnectivity","Enabled",true)`
- **THEN** 链路依次发生：恰好一个 capability 被选中 → SetSwitch(true) 授权并 lowering → 一次物理 tap dispatch → post-dispatch Observation 序列号 > dispatch 前序列号 → 感知证据显示 Enabled=true → 以 SATISFIED 终止且 GoalEvidence.SourceObservationSequence 指向新鲜观测

#### Scenario: 世界已满足则不物理分发

- **WHEN** 世界已为 WiFi ON（初始感知证据即 Enabled=true）
- **THEN** 运行以 SATISFIED 终止且 SHALL NOT 分发任何物理动作（lowerer NoOp 路径，SetSwitch 幂等期望语义）

#### Scenario: 世界状态未知则不盲目分发

- **WHEN** 感知无法确定当前 WiFi 状态（Unknown）
- **THEN** 运行以 STATE_EVIDENCE_REQUIRED 终止且 SHALL NOT 分发物理动作

#### Scenario: dispatch 成功但世界未变不得误判完成

- **WHEN** 物理 tap 已分发且 ADB 返回 Dispatched，但 fresh Observation 感知证据仍显示 Enabled=false
- **THEN** 运行 SHALL NOT 以 SATISFIED 终止（不得将 dispatch 收据当作世界效果证据），并按既有 Traversal 重试/失败语义推进或终止

### Requirement: SetSwitch 保持幂等期望语义且不引入 OS WiFi provider

系统 SHALL 保持 `SemanticActionLowerer` 的幂等期望语义（已满足 → NoOp；未知 → 不分发）；本切片的 WiFi 物理机制 SHALL 保持为「开关坐标处 ADB tap」（DeviceActionTranslator.cs:61-74），SHALL NOT 引入直接 OS WiFi 命令 provider（`svc wifi` / `cmd wifi` / WifiController 类）。

#### Scenario: 幂等语义在真实链路保持

- **WHEN** 真实链路上目标开关已被感知为 ON 且 Goal 要求 Enabled=true
- **THEN** lowering 产出 NoOp，`IEnvironment.ExecuteAsync` 未被调用，ActionHistory 无新增

#### Scenario: 无 OS WiFi provider 引入

- **WHEN** 检索 `src/` 是否存在直接操作系统 WiFi 命令的 provider/controller
- **THEN** 结果为空；WiFi 的物理效果经由 UI 语义环（tap 开关 → screencap → perception → 验证）达成

### Requirement: Emulator 现实校准替换 SYNTHETIC 标记

系统 SHALL 在 emulator 上录制真实 WiFi OFF→ON 转换对（screencap + 感知证据 + 序列），并以录制现实 provenance 替换 `SYNTHETIC_STATE_TRANSITION_PENDING_REALITY_CALIBRATION` 标记（RealitySeededSettingsFixture.cs:138,:147,:158 与 RealitySeededWifiScenarioTests.cs:7-13 所述）。

#### Scenario: 校准资产存在且带 provenance

- **WHEN** 检查校准资产目录
- **THEN** 存在真实 emulator 录制的 OFF 与 ON 成对截图及对应感知证据，并带有录制来源（时间/设备/序列）provenance 记录；SYNTHETIC 标记不再出现在状态转换数据中

#### Scenario: 校准资产可被状态验证复用

- **WHEN** 状态验证测试消费校准资产
- **THEN** `ImageSwitchStateProvider` 对 OFF 资产判定 false、对 ON 资产判定 true，且结果可重复（确定性，无 ML）

### Requirement: Authority 边界与 falsifier 约束成立

系统 SHALL 维持：Agent 为唯一 run-level 语义决策 authority；Traversal 为唯一执行内核；组合根仅做接线、SHALL NOT 做任何语义决策；Provider 永不选择 capability 或判定 Goal；SHALL NOT 引入 provider registry 或 provider selection authority；`TypeLevelDispatchPolicy`（open-world action token 选择）不得被扩展为 provider 选择器。

#### Scenario: 组合根无权决策

- **WHEN** 审查组合根代码
- **THEN** 组合根只含构造与接线（含预检调用），不含 capability 选择、Goal 判定或 recovery 决策逻辑

#### Scenario: 无 registry 引入

- **WHEN** 检索实现中是否存在 provider registry / ServiceCollection / provider 选择表
- **THEN** 结果为空；provider 仅经 `PhysicalEnvironment` 构造注入组合

### Requirement: Runtime 证明要求（每跳 trace 证据）

系统 SHALL 为本切片的每次运行产出贯穿每跳的 trace 证据：Goal → capability → action token → dispatch → Observation 序列号 → verification（遵循 runtime-observability-trace-foundation 的因果链契约：RunId/ContainerId/StepId/ActionId）。

#### Scenario: 闭环可 trace 重放

- **WHEN** 完成一次 OFF→ON 闭环运行
- **THEN** trace 中可重建完整因果链：目标与证据、被选 capability、SetSwitch token、dispatch 动作与收据、post-dispatch Observation 序列号、以及 SATISFIED 的 GoalEvidence.SourceObservationSequence，且各步只追加不改写（I-2）

### Requirement: 实施约束——无隐藏 API、无直接状态改写、无场景状态注入

系统 SHALL NOT 使用 `svc wifi` / `cmd wifi` / 隐藏 emulator API（如 emulator console wifi 命令、UiAutomator 隐藏接口）改变 WiFi 状态；物理效果 SHALL 仅经 UI 语义环（tap 开关 → screencap → perception → 验证）达成。系统 SHALL NOT 直接改写 WorldState/WorldBelief，或绕过 Observation evidence 以任何方式注入世界状态。生产路径 SHALL NOT 接受场景特定状态注入（校准录制资产与 RealitySeeded fixture 数据仅存在于测试侧 Fake 世界）。

#### Scenario: 无隐藏 OS/emulator API

- **WHEN** 检索实现中是否存在 `svc wifi`、`cmd wifi`、emulator console 或 UiAutomator 隐藏接口调用
- **THEN** 结果为空；WiFi 状态变化只经 UI 语义环达成

#### Scenario: 无直接世界状态改写

- **WHEN** 审查状态进入判定的路径
- **THEN** 世界状态仅由 fresh Observation evidence 进入 Agent 判定（裁决 2）；无任何代码路径直接写入 WorldState/WorldBelief 场景字段或伪造证据

#### Scenario: 生产路径无场景状态注入

- **WHEN** 检查生产执行路径对校准资产 / RealitySeeded fixture 数据的引用
- **THEN** 生产路径不消费任何测试侧注入的状态；录制资产仅被测试 Tier 2/3 与文档引用

### Requirement: 陈旧证据不可验证成功

系统 SHALL 仅接受 post-dispatch 的 fresh Observation（序列号相对 dispatch 前推进）作为完成验证证据；陈旧帧（序列未推进或超龄）SHALL fail-closed（`SwitchStateValidation` 陈旧帧语义），SHALL NOT 用于判定 SATISFIED。

#### Scenario: 陈旧帧验证失败

- **WHEN** 使用 pre-dispatch 或序列未推进的 Observation 尝试验证 Goal 完成
- **THEN** 验证以失败/Unknown 呈现（fail-closed），运行不以 SATISFIED 终止

#### Scenario: 序列推进才可验证

- **WHEN** post-dispatch Observation 序列号 > dispatch 前最后观测序列号且感知证据满足 Goal
- **THEN** 验证通过并据此产生 GoalEvidence（fresh evidence）

### Requirement: 失败不得产生语义成功或误触发恢复

provider 或 dispatch 失败 SHALL 以失败或 Unknown 状态呈现（fail-closed 诊断），SHALL NOT 被解释为语义成功；单次动作失败 SHALL 以 `TraversalStepResult.Failed(结构化原因)` 经 Container 转交 Agent 决策（SC-P1-004 escalate 不偷权），恢复 SHALL 仅按 Agent scope 规则进入，不因单次失败自动触发。

#### Scenario: provider 失败不产生成功

- **WHEN** perception 失败（INFRASTRUCTURE_FAILURE/MALFORMED_RESPONSE/SCHEMA_FAILURE/TIMEOUT）或 dispatch 失败（Rejected/TimedOut）
- **THEN** 运行以失败或 STATE_EVIDENCE_REQUIRED（Unknown）状态推进/终止，任何路径不得产生 SATISFIED

#### Scenario: 单次失败不自动触发恢复

- **WHEN** 一次物理动作 dispatch 失败
- **THEN** Traversal 以 Failed(原因) 记录并转交 Agent；恢复机制仅按 Agent 判定的 scope 进入，测试断言该单次失败未误触发恢复流程
