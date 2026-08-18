# Runtime ↔ DSH 架构 Gap Analysis

> Status: GAP_ANALYSIS_COMPLETE (repository-truth based; no code changes)
> Date: 2026-08-17
> Scope: Runtime.Agent + External Intelligence Harness 演进路线的现状差距分析
> Constraint: 不实现代码 / 不创建 OpenSpec change / 不修改 Runtime / 不引入 DSH 类型到 Runtime /
>             不假设未来设计已存在 — 所有结论基于当前 repository evidence

---

## 1. Current Architecture Snapshot

### 1.1 分层现状（仓库真实形态）

| 层 | 组件 | 关键事实（源码证据） |
|---|---|---|
| **Runtime Kernel** | `src/UniClaw.Runtime/` | 零 ProjectReference（Guard 1）、零 LLM/VLM 引用（Guard 2）、无 FSM（I-7）。`Agent` 为纯库组件，全依赖构造注入（`startup / traversal / observeInitial / resolveSemanticPage / containerFactory / recovery / criteria` — `Agent.cs`）。 |
| **Runtime.Agent** | `Agent/Agent.SemanticRun.cs` | 语义闭环 `RunSemanticGoalAsync(SemanticGoalInput, objects, capabilities, runId, ct, maxIterations, viewportExplorationEvaluator, enableDeferredReconciliation)`；单 Run per 实例；fail-closed；GoalEvidence 仅 Kernel 判定（I-10）。公共只读面：`State / Belief / Trace / Reason / RecoveryAnchor / LastTrap / BranchProgress / NavigationEvidence`。 |
| **Environment** | `src/UniClaw.Runtime.Adapters/` | `IEnvironment { ObserveAsync, ExecuteAsync }` 纯端口；`PhysicalEnvironment` 组合 `IScreenshotSource / IPerceptionSource / IAdbDispatchTarget / IStructuredUiHierarchySource`（`PhysicalEnvironment.cs`）。 |
| **DriverHost** | `src/UniClaw.Runtime.DriverHost/` | 独立进程边界；loopback TCP newline JSON-RPC。**8 个冻结只读方法**（`ping/run.list/run.snapshot.get/run.trap.get/run.events.after/run.events.drain/evidence.get/control.support`）+ **1 个新增 run.start**（`UniClawDriverHostServer.Invoke`）。只引用 Runtime + Harness（Guard 10a — 绝不引用 Adapters/PhysicalHost/Vision.Host）。 |
| **Observability** | DriverHost/`Projection/` `Store/` | `AgentStateSnapshot.From(agent)`（公共只读面拷贝）；`RunSnapshotProjector`（DirectPublicProjection / DerivedReadModel / NotCurrentlyAvailable 三分分类）；`RuntimeEventProjector`（18 族事件词汇，A/B/C 分类，C 类永不发射）；`RuntimeEventStore`（per-runId append-only）；`EvidenceCatalog`（逻辑 EvidenceRef 定位）。 |
| **Execution Entry** | DriverHost/`Execution/` | `IUniClawRunExecution.StartRun(RunStartRequest)` + `RunExecutionCoordinator`（DriverHost-owned runId / ONE_ACTIVE_RUN_PER_DEVICE / 异步启动 / 终态 `ReplaceRunProjection`）— dsh-runtime-agent-subagent-run-entry 已实现。 |
| **DSH Plugin** | `dsh-plugin-uniclaw/` | 7 命令（`uniclaw-inspect-run / -trap / -evidence-open / -runs-list / -events-after / -run-goal / -shadow-analyze`）；`adapter.js` TCP 客户端；shadow cognition V1（只读消费 + 可选 `ctx.llm` seam）；control path 零推理调用（F16/F17 guard）。 |
| **生产组合根** | `src/UniClaw.Runtime.PhysicalHost/` | `PhysicalHostComposition`：`ResolveDeviceAsync → BuildRealEnvironment → CreateAttach → BuildRuntimeGraph → HostRuntimeGraph`；新增 `CreateAndroidRunGraphFactory`（DeviceSelector → RunExecutionGraph）+ `BuildDriverHostServer`（只读 surface + 执行协调器 + Android 工厂组合 seam）。 |

### 1.2 已实现的外部交互面（repository truth）

- **Goal Plane 雏形**：`run.start { goal, objects[], capabilities[], device } → RunAccepted(runId)`（异步、DriverHost-owned 身份、`request_rejected` 确定性拒绝）。最小切片：无 TaskSpec、无执行约束字段（`maxIterations` 组合侧默认 5）、无 viewport evaluator 跨 wire。
- **Data Plane 雏形**：`run.snapshot.get`（分类只读快照）、`run.events.after/drain`（游标事件流）、`run.trap.get`、`evidence.get`（逻辑引用）。全部已毕业（dsh-control-plane 系列）。
- **权威边界**：run.start 只传"意图"（goal + 对象 + 能力声明 + 设备选择器）；DSH 无物理/GoalEvidence/binding/belief 权威；Guard 2/10a/10b/10d + `PluginIntegrationGuardTests` + `RunStartExecutionSeam_NotInAgentSemantics` 机械保证。

---

## 2. Target Architecture Model

### 2.1 三层职责模型

```
┌─ Runtime.Agent ─────────────────────────────┐
│ 独立自治执行 Agent；不依赖任何 Harness；      │
│ semantic decision / authorization /         │
│ execution / verification authority           │
└──────────────┬───────────────────────────────┘
               │ External Contract（5 Plane）
┌──────────────▼───────────────────────────────┐
│ Integration Layer / Adapter                  │
│ 协议转换；按外部 Harness 版本维护 binding；    │
│ 隔离 Runtime 与 DSH/Cordis/具体模型实现       │
└──────────────┬───────────────────────────────┘
┌──────────────▼───────────────────────────────┐
│ External Intelligence Harness (DSH)          │
│ General Intelligence Host：LLM/VLM/Subagent/ │
│ Tool/UI/Observation；不直接控制 Runtime 内部 │
│ 状态；不绕过 Runtime execution authority     │
└──────────────────────────────────────────────┘
```

### 2.2 Runtime External Contract（目标 5 Plane）

| Plane | 方向 | 目标消息 | 语义要点 |
|---|---|---|---|
| 1. Goal | External → Runtime | `RunGoal`（Semantic Goal / Object Identity / Desired State） | 意图级，非物理步骤 |
| 2. Data | Runtime → External | `RuntimeSnapshot`（Goal/World/Belief/Progress/Status/Blocker/Artifact refs）；`RuntimeEvent`（Lifecycle/Observation/Execution/Assistance signals） | 证据，非 truth |
| 3. Assistance | Runtime → External | `AssistanceRequest`（semantic interpretation / perception enrichment / candidate ranking / recovery planning / route planning） | **不是调用 LLM，是表达 Runtime 缺少什么能力** |
| 4. Guidance | External → Runtime | `GuidanceProposal`（Hypothesis/Recommendation/Next waypoint/Expected effect/Additional evidence） | Guidance ≠ Truth ≠ Authorization ≠ Goal completion |
| 5. Execution Handoff | Runtime ↔ External | `ExecutionYield`（Runtime 无法安全处理当前交互）；`ExecutionReturn`（外部处理后 Runtime 重新 observe/reconcile） | 临时释放执行租约 |

### 2.3 协作等级（目标）

- **L0 LOCAL** — Runtime 自主完成（当前已实现）
- **L1 CONSULT** — Runtime 请求外部补充信息，Runtime 保留最终决策权
- **L2 DELEGATE_PLANNING** — Runtime 无法规划下一步，外部提供 route/guidance，Runtime 执行验证
- **L3 YIELD_EXECUTION** — Runtime 暂时释放执行租约

### 2.4 演进路线定位

```
Phase A: Runtime as Standalone Agent        ✅ 已实现（语义闭环 + fail-closed）
Phase B: Runtime as Executable Subagent     ✅ 已实现（run.start 垂直切片，本分支当前状态）
Phase C: Runtime Collaborative Agent        ⬜ 未开始（L1 CONSULT / L2 DELEGATE_PLANNING）
Phase D: Runtime + Harness Hybrid           ⬜ 未开始（L3 YIELD / 深度协作）
```

---

## 3. Capability Gap Matrix

分类：`DIRECT_READY`（已有能力直接复用）· `PARTIAL`（已有部分，需补协议/Adapter）· `MISSING`（需新增能力）· `WRONG_BOUNDARY`（已有实现但职责边界需调整）

### A. Runtime External Contract

| Capability | Target Architecture | Current Reality | Status | Gap | Recommendation |
|---|---|---|---|---|---|
| 稳定对外 DTO | 5 Plane 统一契约消息 | `UniClawWireContract` DTO 族（冻结 8+1 方法的 wire copy，分类保留）；Goal Plane 有 `RunStartRequestDto/RunAcceptedDto`；Data Plane 有 `RunSnapshotDto/RuntimeEventDto/EvidenceRefDto`；Assistance/Guidance/Yield 无 DTO | PARTIAL | 有 DTO 但**无"plane"概念的契约骨架**：各 plane 各自为政，无统一 External Contract 文档锚定 | RUNTIME_EXTERNAL_CONTRACT_GATE：把已实现 plane + 目标 plane 边界固化为契约 |
| RuntimeSnapshot/Event contract | Data Plane 稳定只读模型 | 已毕业：`RunSnapshot` 三分分类字段 + 18 族 `RuntimeEvent` + 游标 + 逻辑 EvidenceRef | DIRECT_READY | 无（可作契约基座） | 直接复用 |
| 版本管理机制 | 契约可演进、binding 可对齐 Harness 版本 | `UniClawWireContract.ProtocolVersion = 1`（ping 返回）；无契约级演进策略、无字段级版本、无 backward-compat 声明机制 | PARTIAL | 单一整数版本号，无"如何加 plane / 如何 deprecate 字段"的规则 | 契约 gate 内定义版本策略（protocol baseline 先例可循） |
| Artifact reference 机制 | Snapshot/Event 引用外部资产 | `EvidenceRef`（逻辑定位，非文件路径）+ `evidence.get` + `UniClawCaptureArtifactDto` | DIRECT_READY | 无 | 直接复用 |

### B. Run Entry

| Capability | Target Architecture | Current Reality | Status | Gap | Recommendation |
|---|---|---|---|---|---|
| run.start 授权异步入口 | Goal Plane 入口 | 已实现：`run.start → IUniClawRunExecution → RunExecutionCoordinator → RunAccepted(runId)`；`request_rejected` 与 accepted-then-failed 区分 | DIRECT_READY | 无 | 保持 |
| DriverHost 作为 execution coordinator | 不污染 Agent；协调执行 | `RunExecutionCoordinator` 持有 Agent 任务生命周期/设备保留/runId；`Agent` 只接收既有注入依赖 | DIRECT_READY | 无（Guard 10d 机械保证接缝不泄漏） | 保持 |
| 意图级入口（目标 RunGoal 语义） | Semantic Goal + Object Identity + Desired State | `RunStartRequest { Goal, Objects[], Capabilities[], Device }` — 已含 Goal/Object/State；但无 TaskSpec/执行约束字段（maxIterations 组合侧默认） | PARTIAL | 最小切片未覆盖"执行约束/验收条件"；对 L1+ 协作，run.start 尚不能表达更多意图上下文 | 契约 gate 标注为 deferred 扩展点；有真实消费方再加 |
| 污染 Runtime.Agent | Agent 零 Harness 感知 | 机械保证：Guard 2（零依赖）/ 10a / 10b / 10d；Runtime 源内零 "DSH/DriverHost/run.start" token | DIRECT_READY（无污染） | 无 | 保持 guard |

### C. Data Plane

| Capability | Target Architecture | Current Reality | Status | Gap | Recommendation |
|---|---|---|---|---|---|
| Runtime 事件支撑 Harness 观察 | Lifecycle/Observation/Execution 事件 | 18 族事件（`RunCompleted/RunFailed/TrapRaised/ActionDispatched/…`，A/B/C 分类，C 类永不发射）；`run.events.after/drain` 游标 | DIRECT_READY | 无 | 复用 |
| Projection 层 | 只暴露可审计投影，不暴露内部状态 | `RuntimeEventProjector` + `RunSnapshotProjector` + `AgentStateSnapshot`（仅公共只读面）+ 分类字段 | DIRECT_READY | 无 | 复用 |
| 内部状态直接暴露风险 | 不泄漏 Container/Binding/Belief 内部 | OBS-F10 + Guard 10c：无 ContainerSnapshot 类型、Agent 公共面无新增访问器；BindingsSummary/StateBeliefsSummary = NotCurrentlyAvailable | DIRECT_READY（无泄漏） | 无 | 保持 |
| "Assistance signals" 事件 | Data Plane 含 assistance 相关事件 | 事件词汇无 assistance 族（无 source 可派生 — C 类） | MISSING | 依赖 Assistance seam 先存在（D 检查点） | 在 Assistance seam gate 一并设计，不提前发明事件 |
| Blocker 语义（Snapshot 含 Blocker） | Snapshot 表达运行受阻原因 | `RunSnapshot` 有 ActiveTrap（分类）+ Diagnostics + Reason；无独立的"Blocker"概念 | PARTIAL | Blocker = Trap/Reason 的组合，无显式契约字段 | 契约 gate 评估是否需要显式 Blocker 字段（有消费方再定） |

### D. Assistance Seam

| Capability | Target Architecture | Current Reality | Status | Gap | Recommendation |
|---|---|---|---|---|---|
| Runtime 主动请求外部能力接口 | AssistanceRequest（语义/感知/排序/恢复/路由） | **零实现**：`Agent` 无任何"请求外部"接口；母本 `IntelligenceSeam`（裁决点 consult）仅设计态（`docs/decisions/outer-intelligence-integration-architecture.md` §3） | MISSING | Runtime 侧 seam + 消息格式均不存在 | RUNTIME_ASSISTANCE_SEAM_GATE（改 Runtime，需 OpenSpec + Guard 过审）；先定契约再实现 |
| 异步 request/response correlation | 请求与响应可关联 | 无 correlation 机制；现有事件流只有 EventId/Sequence（投影序） | MISSING | 无 requestId/correlation 概念 | 契约 gate 定义 correlation 形状（对齐 RuntimeEvent.CorrelationId/TraceId 既有字段） |
| world version / observation version 防 stale response | 响应绑定到观测版本 | `Observation.SequenceNumber` 单调递增已存在（可作 world version 基座）；但**无机制把 Assistance 响应绑定到该版本** | PARTIAL | 有版本原语，无绑定机制 | 契约 gate 明确 world version 语义；Assistance seam 强制校验 |

### E. Guidance

| Capability | Target Architecture | Current Reality | Status | Gap | Recommendation |
|---|---|---|---|---|---|
| 外部提供 route/planning suggestion | GuidanceProposal（非权威） | **零实现**：无 Guidance 消息/入口；Phase 6 意图编译未实现（target review 仅设计态） | MISSING | 无协议、无消费点 | L2 阶段 gate；契约 gate 先标注 deferred plane 边界 |
| 保持 Runtime execution authority | Guidance ≠ Authorization ≠ Completion | run.start 已确立先例（DSH 只传意图，Kernel 保留裁决）；`ControlSupportAudit` pause/resume/stop/abort 仍 DEFERRED | DIRECT_READY（先例/约束已确立） | 无（设计 Guidance 时沿用） | 契约 gate 固化为约束条款 |
| 最近邻形态参考 | 外部知识以"建议"进入执行 | `viewportExplorationEvaluator`（调用侧注入 Func，运行时知识，Agent 保留决策）；Recovery 为 Kernel 内机制 | PARTIAL（形态参考） | 注入式 Func 与跨进程 Guidance 不同构 | 未来 Guidance 可参考"Agent 保留裁决"的既有模式 |

### F. Execution Yield

| Capability | Target Architecture | Current Reality | Status | Gap | Recommendation |
|---|---|---|---|---|---|
| 外部临时接管执行边界 | ExecutionYield / ExecutionReturn + lease | **零实现**：`Agent.RunSemanticGoalAsync` 阻塞至终态（单 Run）；run.start 路径传 `CancellationToken.None`（本切片无取消面） | MISSING | 无 yield 语义、无 lease、无 reconcile 重入点 | 远期（Phase C/D）；不阻塞当前路线；契约 gate 标注 deferred |
| lease/reconcile 机制 | 释放租约后重 observe | 无 | MISSING | 同上 | 同上 |

### G. DSH Adapter

| Capability | Target Architecture | Current Reality | Status | Gap | Recommendation |
|---|---|---|---|---|---|
| 命令/工具面 | Harness 通过 tool/UI 驱动 | 7 命令（6 只读 + run-goal）+ shadow V1；`inject: ['commands']` 真实 host 回归（7 命令 registry） | DIRECT_READY | 无 | 复用 |
| Integration Service 层 | 协议转换 + 按 Harness 版本维护 binding + 隔离 | 当前为 thin adapter（`adapter.js` TCP client）+ commands + shadow facade；**无独立 Integration Service 层**，wire 方法直接映射到 adapter | MISSING | 无协议转换/版本 binding 层；多 plane 扩展后 adapter 会变胖 | DSH_ADAPTER_ALIGNMENT_GATE：在契约固化后引入 Integration Service（版本 binding、消息适配），保持 adapter 薄 |
| DSH 协议污染 Runtime 风险 | 隔离 Runtime 与 DSH/Cordis/模型 | Guard 2/10a/10b/10d + `PluginIntegrationGuardTests`（A/B/C/D/F/F2）+ node F16/F17：Runtime 零 DSH token；DriverHost 词汇受限；control path 零推理引用 | DIRECT_READY（隔离机制已机械保证） | 风险点：**契约无锚点** — 各 plane 若各自实现 wire 格式，隔离靠 guard 逐条打补丁 | 契约 gate 把 plane 边界/词汇固化为唯一锚点 |

---

## 4. Protocol Gap

### 4.1 已实现协议（repository truth）

| Plane | 方法 | 状态 |
|---|---|---|
| Goal（雏形） | `run.start`（RunStartRequest → RunAccepted） | ✅ 实现（additive，9th 方法） |
| Data | `run.list / run.snapshot.get / run.trap.get / run.events.after / run.events.drain / evidence.get / control.support / ping` | ✅ 已毕业（冻结） |

### 4.2 缺失协议

| Plane | 目标消息 | 缺失项 |
|---|---|---|
| Assistance | `AssistanceRequest` + response + correlation | 消息格式、异步 correlation、world-version 绑定、Runtime 侧 seam — 全缺 |
| Guidance | `GuidanceProposal` | 消息格式、消费点 — 全缺 |
| Execution Handoff | `ExecutionYield / ExecutionReturn` | 消息格式、lease 语义 — 全缺 |
| 版本机制 | 契约演进规则 | 仅 `ProtocolVersion=1` 单一整数；无 plane 扩展/deprecation 规则 |

### 4.3 结论

协议骨架（goal + data）已存在且高质量（分类字段、游标、逻辑引用），但**未以"Runtime External Contract"名义固化**：没有统一的 plane 划分文档、没有契约级版本演进策略、没有 assistance/guidance/yield 的 deferred 边界声明。这是当前最真实、成本最低、顺序最靠前的协议缺口。

---

## 5. Ownership Boundary Review

### 5.1 现状权威边界（全部有机械保证）

| 边界 | 现状 | 证据 |
|---|---|---|
| DSH 无物理权威 | run.start 只传意图；无坐标/DeviceAction/ElementIndex | `RunStartRequest` 字段 + F8 falsifier |
| DSH 无 GoalEvidence 权威 | 完成只来自 Kernel（`RunCompleted/RunFailed`、GoalEvidence） | `RuntimeEventProjector` Phase 4 + `DSHCompletionAuthority = NONE` |
| DSH 无 binding/belief 权威 | Container 状态私有；Snapshot 分类 NotCurrentlyAvailable | OBS-F10 + Guard 10c |
| Agent 不依赖 Harness | Runtime 零 ProjectReference、零 DSH token | Guard 1/2/10b + `PluginIntegrationGuardTests.GuardA/B` |
| DriverHost 不被插件拥有 | 插件 CONNECT，不 launch/supervise | 冻结 process-lifecycle 决策 + F6 falsifier |
| 执行接缝不泄漏 | `IUniClawRunExecution` 独立于只读 surface | Guard 10d |

### 5.2 目标协作等级的新边界要求（设计约束，非现状缺口）

- **L1 CONSULT（Assistance）**：Runtime 主动请求、外部只补信息、Runtime 保留最终决策 —— 与 I-3（Agent 唯一语义裁决 authority）兼容，需在 seam 设计时保证"建议制"（母本 §3 已预判）。
- **L2 DELEGATE_PLANNING（Guidance）**：外部给 route/guidance、Runtime 执行+验证 —— 必须显式声明 "Guidance ≠ Truth ≠ Authorization ≠ Goal completion"（目标架构已定义）。
- **L3 YIELD**：lease 语义 —— 需要新的执行租约模型，超出当前 RunState（Idle/Initializing/Running/Completed/Failed/Terminated-reserved）。
- 结论：**当前边界模型与目标架构无冲突**；L1/L2/L3 的新边界属于契约 gate 的设计条款，不构成对现状的修正需求（无 WRONG_BOUNDARY 项）。

---

## 6. Adapter Boundary Review

### 6.1 当前 Adapter 形态

`dsh-plugin-uniclaw` = **thin protocol adapter + command registry + shadow facade**：
- `adapter.js`：loopback TCP JSON-RPC 客户端（fresh-state reconnect、typed errors、`runStart` 为唯一执行方法）
- `commands.js`：7 个确定性命令（零推理调用）
- `shadow/`：只读消费 + 可选 `ctx.llm`（EPHEMERAL_PROCESS_LOCAL）

### 6.2 与目标 Integration Layer 的差距

| 目标 | 现状 | Gap |
|---|---|---|
| 协议转换 | wire 方法 ↔ DTO 直接映射 | 无独立转换层（薄 adapter 尚可，多 plane 后不足） |
| 按 Harness 版本维护 binding | `DSH_BASELINE` 常量 pin 到单个 commit | 无"多版本 binding"机制（当前单 pin 够用） |
| 隔离 Runtime 与 DSH/Cordis/模型 | Guard 机械隔离 | ✅ 已满足；风险在契约无锚点 |

### 6.3 风险判断

- **当前无协议污染**（Guard 证据充分）。
- **未来风险**：若在契约固化前直接实现 assistance/guidance 消息，plugin 与 Runtime 的 wire 格式会各自生长，隔离 guard 需逐条打补丁 —— 这是选择 RUNTIME_EXTERNAL_CONTRACT_GATE 的直接理由。
- 推荐：契约 gate 先固化 plane 边界 → 再实现 Integration Service 层（DSH_ADAPTER_ALIGNMENT_GATE）→ 再实现 Runtime seam（RUNTIME_ASSISTANCE_SEAM_GATE，顺序上 seam 需契约先行）。

---

## 7. Recommended Next Gates

| Gate | 内容 | 前置 | 风险 | 顺序 |
|---|---|---|---|---|
| **RUNTIME_EXTERNAL_CONTRACT_GATE** | 把 goal+data plane 现状 + 目标 5-plane 边界固化为 External Contract（契约文档、DTO 版本策略、correlation/world-version 原语定义、deferred plane 声明）；不改 Runtime 语义 | 无 | 低（契约层，可走 protocol-baseline 先例） | 1 |
| **RUNTIME_ASSISTANCE_SEAM_GATE** | L1 CONSULT：Runtime 侧 `AssistanceRequest` seam（OpenSpec change；改 Runtime 需过 Guard/14 Invariants）+ correlation + world-version 校验 | 契约 gate（消息格式/correlation 已定义） | 中（改 Runtime） | 2 |
| **DSH_ADAPTER_ALIGNMENT_GATE** | Integration Service 层：版本 binding、assistance/guidance 消息适配、保持 adapter 薄 | 契约 gate | 低-中 | 3（可与 2 并行） |
| Guidance / Yield gate | L2 / L3 协作（GuidanceProposal、ExecutionYield lease/reconcile） | 契约 + seam | 高（新执行模型） | 4+（远期，Phase C/D） |

---

## NEXT_GATE_RECOMMENDATION

**`RUNTIME_EXTERNAL_CONTRACT_GATE`**

### 理由（repository-truth based）

1. **第一个真实缺口是"契约无锚点"，不是"能力缺失"**：Goal Plane（run.start）与 Data Plane（snapshot/events/evidence）已实现且高质量，但从未以统一 External Contract 固化 — 无 plane 划分、无契约级版本演进策略、无 deferred 边界声明（A 检查点 PARTIAL；§4.3）。
2. **治理先例与时机**：项目惯例是"protocol baseline 先冻结 → 再实现"（dsh-uniclaw-control-plane-protocol-baseline → plugin implementation）。run.start 刚成为第一个 mutating 方法，正是把"已实现 plane + 未来 plane 边界"固化成契约骨架的窗口。
3. **防止协议污染扩散**：Assistance/Guidance/Yield 三个 plane 全部 MISSING（D/E/F 检查点，grep 零实现）。若先实现 seam 或 adapter 而不先定契约，各 plane 的 wire 格式会各自生长，DSH 协议污染 Runtime 的风险（G 检查点）从"机械可控"变为"需逐条打补丁"。契约 gate 是唯一的低风险锚点。
4. **不是其他候选**：
   - 非 `NO_GAP_CURRENT_ARCHITECTURE_READY`：三个 plane MISSING + 版本机制 PARTIAL，差距明确。
   - 非 `RUNTIME_ASSISTANCE_SEAM_GATE`（次优）：seam 是 Runtime 修改（高风险），且消息格式/correlation/world-version 需契约先定义；顺序上契约必须先行。
   - 非 `DSH_ADAPTER_ALIGNMENT_GATE`（次优）：adapter 对齐依赖契约边界（§6.3）；当前 thin adapter 无紧迫缺口。
5. **约束合规**：该 gate 只固化契约（文档 + DTO/版本策略），不修改 Runtime、不引入 DSH 类型到 Runtime、不创建 OpenSpec change（本分析仅建议未来 gate 名称）。

---
