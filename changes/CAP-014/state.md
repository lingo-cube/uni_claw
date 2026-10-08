# CAP-014 — Agent 任务初始化携带能力选择

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: working-tree

## Intent（WHAT/WHY）

把 CAP-013 的能力选择从 `submit_decision` 首次回执中移到 Agent 下发任务的
通用初始化外壳。Agent 下发计划或指令时都必须先带任务初始化；Host 先消费初始化
并形成 task-scoped binding，再消费任务载荷。这样首次下发不依赖具体动作工具，
也不会把能力选择误当成动作决策字段。

## Scope

- 定义 transport-neutral 的 `AgentTaskEnvelope` / `AgentTaskInitialization`。
- 让计划、指令等任务载荷共用同一初始化位置。
- Host/DSH 只接受初始化中的能力选择；移除 `submit_decision` 顶层选择字段。
- 保留任务写死优先、必选未绑定安全停止、可选未选择不运行的 CAP-013 语义。
- 更新 ADR、CONTEXT、CAP-013 关联记录和协议/插件/测试证据。

## Out of Scope

- 不新增第二套任务系统或改变 Kernel 的 AgentDecision union。
- 不让 runner 直接从 Registry 代选能力。
- 不把语言 Finding 升格为权威事实。
- 不扩展新的语言检查能力实现。

## Decisions

1. 任务初始化属于 Agent→Host 的通用任务外壳，不属于 `submit_decision`。
2. 任务载荷可以是计划、指令或现有决策投影，但都共享同一个初始化位置。
3. 初始化最多消费一次；后续任务重复改选能力时 Host fail-closed。
4. 任务固定 capability id 优先于初始化中的 Agent 选择。

## Acceptance

1. 协议模型存在通用任务外壳，初始化中承载 capability selection，计划/指令不各自复制字段。
2. `submit_decision` schema、插件和 adapter 不再暴露顶层 `capabilitySelection`。
3. Host 只从任务初始化读取一次选择并完成现有 language inspection binding。
4. 旧 CAP-013 binding、Settings Coverage、DSH 和全解测试继续通过。

## Verification

| level | method | expected | actual | evidence |
|---|---|---|---|---|
| CONTRACT | 协议模型/schema/ADR 对照 | 初始化与任务载荷分层，选择不属于 submit_decision 动作字段 | PASS | `src/UniClaw.Agent.Dsh/ProtocolModels.cs`; `src/UniClaw.Agent.Dsh/ProtocolSchema.cs`; `docs/adr/0040-agent-selected-task-capability-binding.md` |
| DETERMINISTIC | Agent.Dsh + Host focused tests | 首次任务初始化绑定；重复初始化 fail-closed；固定任务优先 | PASS；Agent.Dsh.Tests 158/158，Host.Tests 197/197 | `tests/UniClaw.Agent.Dsh.Tests/DshOpenedHttpPeerTests.cs`; `tests/UniClaw.Agent.Dsh.Tests/ProtocolFoundationTests.cs`; `src/UniClaw.Host/HostRunner.cs`; `src/UniClaw.Host/SettingsCoverage/SettingsCoverageRunner.cs` |
| SCENARIO | Host.Tests、plugin tests、full solution | CAP-013 既有语义无回归 | PASS；plugin 22/22；full solution 1440/1440 | `dsh/uniclaw-decision-channel/tests/plugin.test.mjs`; `dotnet test UniClaw.Kernel.slnx --no-restore` |

## Status log

- 2026-10-06 · UNDERSTAND → RESOLVE → PERSIST · 所有者修正：初始化边界是
  Agent 下发任务时，无论载荷是计划还是指令都必须携带；旧的首次
  `submit_decision` response 方案作废。
- 2026-10-07 · IMPLEMENT → VERIFY · 新增 `AgentTaskEnvelope`，把初始化与
  payload 分层；顶层 `capabilitySelection` 已拒绝；Host 在 payload 进入 Kernel
  前绑定能力。Agent.Dsh 157/157、Host 195/195、plugin 22/22、全解 1437/1437。
- 2026-10-07 · REVIEW → CLOSED · 协议生成物、Host/DSH 适配、插件和文档已同步；
  无本 change 范围内的未验证项。
