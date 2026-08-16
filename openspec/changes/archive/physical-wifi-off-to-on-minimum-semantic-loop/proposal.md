# Proposal: Physical WiFi OFF→ON Minimum Semantic Loop（第一条真实 Agent Vertical Slice）

| 属性 | 内容 |
|------|------|
| Change ID | `physical-wifi-off-to-on-minimum-semantic-loop` |
| 状态 | Proposed（**本 change 只产出 proposal / design / specs / tasks，不实施**） |
| 类型 | Vertical Slice（Provider Foundation 后的第一条真实环境闭环） |
| 日期 | 2026-08-12 |
| 分支 | `uni-agent` |
| 上游 | `docs/decisions/provider-foundation-reconciliation.md`（Provider Matrix + 根因分析） |
| Authority | `PROJECT_LEADER_CREATE_OPENSPEC_PHYSICAL_WIFI_OFF_TO_ON_MINIMUM_SEMANTIC_LOOP_GATE` |

> **验收约束**：本 change 完成后运行 `openspec validate physical-wifi-off-to-on-minimum-semantic-loop` 通过即停（STOP after OpenSpec validation）。任何实现都需另行授权。

## Why

Provider Foundation Reconciliation（`docs/decisions/provider-foundation-reconciliation.md`）已定位唯一根因：provider 层（Screenshot / ADB Dispatch / Vision）**代码完整、隔离测试充分，但从 Runtime 视角是不可达的死代码** —— 不存在生产组合根（`src/` 全库无 `Program`/`Main`/DI；`PhysicalEnvironment` 仅在测试中用 stub 构造），Agent 运行的 `IEnvironment` 永远是测试侧 Fake。第一条真实垂直切片必须打通

**UserGoal → SemanticGoal → Agent Decision → Capability Selection → Action Token → Provider Dispatch → Physical Effect → Fresh Observation → Perception Evidence → State Verification**

的完整闭环，用「真实 WiFi 开关 OFF→ON 并验证世界状态」作为验收对象。这同时是 Main Roadmap 的「Agent Semantic Loop」在真实环境上的第一步。

## What Changes

- **生产组合根（新增）**：新建薄宿主项目/入口，用普通构造注入（无 DI/registry）组合
  `AdbDeviceResolver/Preflight → PhysicalEnvironment(AdbScreenshotSource, LocalVisionPerceptionSource, ImageSwitchStateProvider, AdbDispatchTarget) → Startup/Traversal/Recovery/Agent`，
  并注入 `IEnvironment` 给运行内核。Runtime 核心保持零 ProjectReference（Guard 1 不受影响）。
- **Fake→Real 过渡（显式边界）**：`PhysicalEnvironment` 从「仅测试构造」变为「生产可构造」；测试侧 Fake
  （`ScriptedEnvironment` / `ReplayEnvironment` / `SimulationEnvironment`）保持原样并全绿 —— 真实/虚假环境的**选择权只存在于组合根**，Runtime 逻辑内不出现任何 switch/flag。
- **真实 IO 落地的 deferred seam（最小 Runtime 改动）**：`Traversal` sync-over-async（Traversal.cs:39-41 自带
  「Phase 4 接入真实 IO 时改为异步形状」裁决标记）与 `Startup.AttachAsync` 空实现（Startup.cs:99-104）在本切片落地。
- **Emulator 现实校准**：录制真实 emulator（emulator-5554，与既有 committed assets 一致）的 WiFi OFF→ON 转换对，
  替换 `SYNTHETIC_STATE_TRANSITION_PENDING_REALITY_CALIBRATION` 标记（RealitySeededSettingsFixture.cs:138,:147,:158），
  并为 `ImageSwitchStateProvider` 提供 live-frame 校准。
- **语义闭环端到端**：`SemanticGoalInput("WifiConnectivity","Enabled",true)` 全链执行，仅当**新鲜 post-dispatch
  Observation 证据**显示 Enabled=true 时以 SATISFIED 终止（I-10 / 裁决 10：dispatch 收据 ≠ 世界效果证据）。
- **最小 provider 实现**：仅限使既有链路可运行所需（如缺失的连接参数透传）；**不**新增直接 OS WiFi 命令 provider
  （`svc wifi`/`cmd wifi`）——物理机制保持 SetSwitch → 开关坐标处 tap（DeviceActionTranslator.cs:61-74）。

## Capabilities

### New Capabilities

- `physical-wifi-off-to-on-minimum-semantic-loop`: 生产组合根 + 真实 provider 接线 + emulator 现实校准 +
  WiFi OFF→ON 语义闭环（Goal → … → State Verification），含 authority 边界与 falsifier 约束。

### Modified Capabilities

无（本仓库无 `openspec/specs/` 主规格；既有 phase1 `normal-wifi-scenario` 为 Fake-only 场景，本 change **不改动**该规格 —— 真实闭环是新能力，不是对 Fake 场景的修改）。

## Impact

- `src/UniClaw.Runtime/`：`Traversal`（async 化，最小改动）、`Startup`（Attach 落地）—— 仅此两处核心改动，语义 authority 不变。
- `src/UniClaw.Runtime.Adapters/`：仅组合接线与必要的可见性调整；provider 机制代码不改语义。
- 新宿主项目（组合根入口，引用 Runtime + Adapters）。
- `tests/UniClaw.Runtime.Tests/`：新增真实环境集成 tier（不并入普通 Fake 套件）、校准资产与 provenance 记录。
- 文档：宪章 §33 门（真实设备接入时机）的 emulator-only 决策记录；`docs/decisions/`。
- 依赖：无新增外部依赖；emulator/adb 为环境前置（preflight 显式失败）。

## 非目标（Forbidden / Deferred，本 change 明确不做）

- ❌ Provider registry / provider selection authority（设计文档明令禁止：trace-capture-scenario-catalog-foundation/proposal.md:33 等）
- ❌ 通用 workflow engine / 独立 Runner / production subsystem（裁决 7：单一 Runtime slice）
- ❌ Runtime 语义 authority 扩展（Agent 仍是唯一决策 authority；组合根无权决策）
- ❌ 替换 Capability 模型（`Capability(Name, ApplicableToCategory, StateDimension)` 原样保留）
- ❌ training / perception 算法变更（`ImageSwitchStateProvider` 为确定性 luminance heuristic，不改算法，只做校准）
- ❌ ReleasePolicy / Candidate-vs-ACTIVE / 真实手机（本切片限定 emulator；宪章 §33）
- ❌ LLM / Memory / Phase 5+ 能力

## 验收

- `openspec validate physical-wifi-off-to-on-minimum-semantic-loop` 通过（本 change 的唯一交付验收）。
- spec 每条 SHALL 可被 falsifier 或 scenario 测试断言；falsifiers 列表见 spec.md 与 design.md。
- Runtime 核心（Guard 1：零 ProjectReference）不被破坏；既有 411/411 场景与 Perception 全绿套件不回归。
