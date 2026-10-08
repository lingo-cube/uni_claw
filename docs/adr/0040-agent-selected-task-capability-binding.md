# ADR-0040：由 UniAgent 选择 Runtime Integration 能力并形成任务绑定

状态：accepted（2026-10-06，CAP-013）

CAP-012 已完成语言检查能力本身，但把“任务配置存在”直接当成“本次启用”会让
Host/runner 越过 UniAgent 的选择权。CAP-005 同时明确：任务级 binding 接口尚未
冻结，Registry 的常驻 `Resolve()` 不能代替任务装配。本决策冻结两者之间的职责
边界，wire payload 由 CAP-014 的任务 envelope 和 CAP-015 的首次请求上下文投影
共同固化。

## Decision

1. Runtime Integration Registry 提供能力集、能力描述和 profile/facts 给
   `uni-agent`；已注册不等于本次任务已启用。
2. 任务 profile 只声明能力要求和参数。语言检查的最小参数是 `required`、
   `expectedLanguage` 和精确的 `ignoreRoutes`；不提供 Host 侧 `enabled` 开关，
   也不从设备 locale 推断期望语言。全局语言白名单继续由独立 profile 文件提供。
3. 能力有两种选择入口：任务可以直接写死 capability id 和参数，也可以只写
   要求、由 `uni-agent` 从能力集选择。Agent 下发任务时统一使用
   `AgentTaskEnvelope`，把一次性的 `capabilitySelection` 放在
   `task.initialization`，把计划、指令或其他任务载荷放在 `task.payload`。
   任务已经写死时优先，Host 校验信任域、协议、配置和 Run 关联后创建本次任务的
   task-scoped binding；runner 只消费这个已选 binding，不能自行从 Registry 代选
   或按配置隐式打开。后续任务不得重复改选能力。
4. `required=true` 但没有有效 binding 时，任务安全停下并披露“能力未绑定”；
   可选要求未被选择时不运行能力，也不生成空的语言检查产物。
5. 已绑定的语言检查在 observation commit 之后消费有界文本投影：层级的
   `text`、`content-desc`、`hint` 是独立 Declared Text，不冒充 Rendered Text。
   命中精确 route 的 capture 产出 `NotApplicable`；没有文字或输入不完整产出
   `Unknown`；检查/持久化故障隔离为诊断并保留主 Runtime 继续运行。

## Consequences

- Capability set 的消费者是 UniAgent；Host 只负责验证和装配，避免配置文件绕过
  agent 选择形成第二条 authority path。
- CAP-012 D5 的 runner 接线必须以 task-scoped binding 为输入；在 binding
  wire shape 验证前，不能把 `RegisterLanguageInspector().Resolve()` 直接塞进
  runner。
- `capability-profiles.json` / `capability-facts.json` 是 agent 的能力发现事实；
  运行时首次 Agent 请求把当前可用 profile map 放进
  `AgentDecisionContext.availableCapabilities`，后续请求不重复发送；本决策不
  新增语言检查专用 Tool。
- task initialization 中 selection 的最小字段是 `capabilityId`、可选
  `expectedLanguage` 和 `ignoreRoutes`；它属于通用任务 envelope，不是动作
  proposal。binding 生命周期仍由 CAP-013 Host seam 管理；能力集的实时 context
  投影由 CAP-015 固化为首次 Agent 请求的上下文字段。

## Rejected alternatives

- **只有配置就自动启用**：越过 UniAgent 选择权，并把 task profile 变成第二个
  Capability Owner；只有任务明确写死时才允许配置直接成为选择结果。
- **runner 每次自行 `Resolve()`**：复用全局常驻实例，无法表达 task-scoped
  配置、Run correlation 和关闭边界。
- **设备 locale 作为期望语言**：仓库没有稳定的 locale 证据，且会把环境事实
  误当任务要求。
