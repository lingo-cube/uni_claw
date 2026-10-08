# CAP-015 — 首次 Agent 请求注入可选择能力集

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: working-tree

## Intent（WHAT/WHY）

CAP-013 已经定义“能力集供 uni-agent 选择”，CAP-014 已把选择放进 Agent
任务初始化。还缺最后一段：Agent 第一次被调用时必须拿到当前可用能力的事实，
否则它只能凭空猜 capability id。能力集只随本次 Run 的第一次请求注入；后续请求
沿用任务级 binding，不重复发送或重新选择。

## Scope

- 在 `AgentDecisionContext` 增加可选的 `availableCapabilities` profile map。
- Host/Settings Coverage 第一次调用 Agent 前，从 Runtime Integration Registry
  投影当前仍可用、可执行且能自述 profile 的能力。
- DSH prompt 明确能力集只在首次上下文出现，选择只能使用其中的 key。
- 同步协议 schema、TypeScript、hash、测试和 CAP-013/ADR 记录。

## Out of Scope

- 不让 Kernel 直接读取 Registry 或代替 Host 选择能力。
- 不在后续 Agent 请求重复注入能力集，不改变 task initialization 一次性选择语义。
- 不新增能力实现、Tool 或第二套能力发现文件。

## Decisions

1. 能力发现事实属于 `AgentDecisionContext.availableCapabilities`，由 Host 在首次
   Agent 请求前注入；Kernel 仍只负责构建基础决策上下文。
2. map key 是注册能力 ID，value 是实例 `DescribeProfile()` 的事实报告；只投影
   `Registered/Ready/Active` 生命周期，关闭或失败能力不进入选择集。
3. 首次请求即使无响应，也算已发送过发现上下文；后续请求不重新发送能力集。
4. Agent 仍只负责表达任务级 selection，Host 负责校验、绑定和安全停止。

## Acceptance

1. 首次 Agent 请求上下文包含当前 Runtime Integration 能力 profile map；无注册表
   时传空 map，不伪造能力。
2. 后续 Agent 请求不重复带 `availableCapabilities`；任务初始化 selection
   仍只消费一次。
3. DSH schema/plugin/prompt 能传输并解释该字段，Agent 只能选择 map 中的 ID。
4. 既有 CAP-013 binding、CAP-014 task envelope、Host、Agent.Dsh 和全解测试回归通过。

## Verification

| level | method | expected | actual | evidence |
|---|---|---|---|---|
| CONTRACT | `AgentDecisionContext` + generated schema | `availableCapabilities` 是可选 map，值为 `CapabilityProfileReport` | PASS | `src/UniClaw.Kernel/Runtime/AgentDecision.cs`; `schemas/agt-002/product-protocol.schema.json` |
| DETERMINISTIC | Runtime Integration composition tests | 只投影当前可执行 profile，关闭后移除 | PASS；5/5 | `tests/UniClaw.Host.Tests/RuntimeIntegrationCapabilityCompositionTests.cs` |
| DETERMINISTIC | ProtocolFoundation + Agent.Dsh tests | schema/hash/dts 同步，任务初始化一次性选择保持 | PASS；22/22 | `tests/UniClaw.Agent.Dsh.Tests/ProtocolFoundationTests.cs` |
| SCENARIO | Host + full solution | 首次注入不破坏绑定/执行链 | PASS；full solution 1440/1440 | `src/UniClaw.Host/HostRunner.cs`; `src/UniClaw.Host/SettingsCoverage/SettingsCoverageRunner.cs`; `dotnet test UniClaw.Kernel.slnx --no-restore` |

## Status log

- 2026-10-07 · UNDERSTAND → RESOLVE → PERSIST · 所有者确认能力集放在第一次
  Agent 请求；后续请求不重复注入。
- 2026-10-07 · IMPLEMENT · `AgentDecisionContext.availableCapabilities`、Host
  首次请求投影、Runtime Integration profile map 和 DSH prompt/schema 已接线。
- 2026-10-07 · REVIEW → VERIFY → CLOSED · schema/hash/dts、plugin 22/22、Host
  focused 5/5、Agent.Dsh 22/22、scenario certification 20/20 blocks、full
  solution 1440/1440 通过；保留既有 NU1900 cache warning。
