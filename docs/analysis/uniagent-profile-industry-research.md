# UniAgent Profile 业界一手资料研究

> 状态：research note，仅用于设计输入，不授权产品实现。
> Status: DRAFT（梳理/调研产物，未授权实现）
> Authority: `NONE`（分析产物，不是架构决策；与 ADR / baseline 冲突时以后者为准）
> 日期：2026-10-07
> 范围：Agent Profile 的职责边界、配置/版本、模型、工具、记忆和安全策略。

## 结论先行

业界的 `Agent` 配置通常不是“一个 prompt + 一个模型名”，而是一个可装配的
运行契约：身份/说明、模型与模型参数、工具与委派、上下文/会话、输出契约、
生命周期观测和安全门。OpenAI Agents SDK 将这些字段集中在 Agent 定义中，并把
Responses API 的 prompt 版本引用、session、guardrail 和 handoff 分开表达；Google
ADK 则把模型、instruction、tools 作为最小 Agent 单元，再向 session/state/memory
和 workflow 扩展；AutoGen 的声明式配置进一步把 model client、tools、handoffs、
model context、memory、迭代上限等作为可序列化组件。

对 UniClaw，Profile 应该是 **UniAgent realization 的可验证装配描述**，而不是
Goal、Execution Contract、Runtime Outcome 或 Kernel policy 的第二个所有者。它应
声明“本 realization 可用哪些模型/产品工具/记忆适配器/安全门，以及如何版本化和
观测”，但不能把宿主能力提升为 Product authority，也不能绕过 Uni Kernel 的
Effect Boundary。

## 一手来源与可复用事实

### 1. OpenAI Agents SDK：Agent 是装配根，Responses prompt 可版本化

官方 Agent 文档把 Agent 定义为“LLM + instructions + tools”，并列出 name、model、
model settings、tools/MCP、input/output guardrails、output type、hooks、handoff
和 tool-use behavior 等可选配置；SDK 默认用 Responses API，并由 Runner 管理
turn、tools、guardrails、handoffs 和 sessions。[OpenAI Agents SDK — Agents](https://openai.github.io/openai-agents-python/agents/)

同一文档支持把 Responses prompt 写成 `{id, version, variables}`，也支持运行时
动态生成 prompt 引用；这表明 prompt 应有独立身份和版本，而不是把长文本直接
散落在 Agent Profile 中。文档还把 `context` 定义为由调用方注入、传给 agent/tool/
handoff 的依赖与状态容器，说明“运行上下文”应与静态 Profile 分层。[Prompt templates and Context](https://openai.github.io/openai-agents-python/agents/#prompt-templates)

工具文档将 hosted tools、local/runtime tools、function tools、agents-as-tools
和实验性 Codex tool 分开，并列出 hosted web/file/code/MCP/tool-search 等类别；
工具的执行位置和信任边界因此是配置的重要部分，而不只是字符串 allowlist。[OpenAI Agents SDK — Tools](https://openai.github.io/openai-agents-python/tools/)

**对 Profile 的启示：**

- `identity`、`instructionsRef`、`model`、`modelSettings`、`tools`、`handoffs`、
  `guardrails`、`outputContract`、`hooks` 应是不同字段，不合并成一段 prompt。
- prompt、工具清单和 Profile 版本都应能被审计；运行上下文/session 属于一次
  Product Session，不应写入不可变 Profile。
- 工具字段至少要表达来源/执行面（hosted、adapter、capability-backed）和
  tool-choice/循环上限。

### 2. Google ADK：从最小 Agent 到 workflow、session/state/memory

ADK 官方 Agent 文档将 LlmAgent 的基本组件明确为 AI model、task instructions 和
可选 tools；当复杂度增加时再组合成 workflow，并强调拆分有助于控制上下文限制、
模块化代码和协调多 Agent。[Google ADK — Agents](https://adk.dev/agents/)

ADK 的官方文档导航把 session、state、events、memory、context compression 和
model-context caching 作为 Agent context 的不同能力面，而不是一个含糊的
“memory”开关。[Google ADK — Agent context / Sessions / Memory](https://adk.dev/agents/)

ADK 还提供独立的 Agent Config reference，说明配置格式/字段是可被工具消费的
契约，而不是只存在于构造函数里的实现细节。[Google ADK — Agent Config reference](https://adk.dev/reference/agent-config/)

**对 Profile 的启示：**

- 先定义单 Agent Profile 的最小闭包；workflow/多 Agent 委派应由独立的
  orchestration 配置引用 Profile，而不是把所有编排塞进一个 Profile。
- `sessionState`、短期 `context`、长期 `memory`、压缩/cache 要有不同适配器
  和生命周期；Profile 只声明绑定方式、scope 和保留策略。
- Profile schema 应有稳定版本并可被 CLI/validator 读取，便于在 realization
  替换时保持同一 Product conformance surface。

### 3. Anthropic：工具是有 schema 的能力边界，执行权仍在应用；严格模式和批准是安全面

Anthropic 官方 tool-use 文档说明：自定义工具由应用声明 schema 并执行调用，模型
只产生 `tool_use`，应用返回 `tool_result`；`strict: true` 可保证调用符合 schema。
文档也区分 client tools（应用执行）和 server tools（Anthropic 基础设施执行），
并列出 memory、bash、text editor、computer/browser、web search/fetch、code
execution、MCP 等不同工具边界。[Anthropic — Tool use with Claude](https://platform.claude.com/docs/en/agents-and-tools/tool-use/overview)

文档规定默认 `tool_choice=auto`，也支持要求/禁止工具调用；同时提示当必需参数
缺失时模型可能询问，也可能猜测，不能把模型行为当作参数校验。[Tool choice and strict tool use](https://platform.claude.com/docs/en/docs/agents-and-tools/tool-use/overview#when-claude-uses-tools)

**对 Profile 的启示：**

- 每个工具引用必须有 schema/version、执行主体、输入校验、错误策略和 approval
  要求；不能只保存工具名。
- “模型可以请求调用”与“现实动作得到授权/执行”必须分离。UniAgent Profile
  只声明产品工具词汇及所需安全门，实际 capability binding/effect delivery
  仍由 adapter/Kernel 负责。
- `toolChoice`、strict schema、最大迭代次数、超时和人工确认是安全策略字段，
  不是 prompt 建议。

### 4. Microsoft AutoGen：声明式 Component 配置覆盖模型、工具、上下文和记忆

AutoGen `AssistantAgentConfig` 源码把 `model_client`、`tools`、`workbench`、
`handoffs`、`model_context`、`memory`、`system_message`、streaming、tool-use
reflection、`max_tool_iterations`、metadata 和 structured message factory
作为配置字段，并以 ComponentModel 表达可加载组件。[AutoGen AssistantAgentConfig source](https://github.com/microsoft/autogen/blob/main/python/packages/autogen-agentchat/src/autogen_agentchat/agents/_assistant_agent.py)

同一源码明确 agent 在调用间保持自身状态，调用方只传递新消息；工具可并发执行，
`max_tool_iterations` 限制单次运行的连续工具回合，且不可序列化的 formatter 不
会进入 YAML/JSON 配置。这些约束说明可持久化 Profile 必须区分“可声明配置”和
“进程内回调/运行态对象”。[AutoGen state and tool-loop behavior](https://github.com/microsoft/autogen/blob/main/python/packages/autogen-agentchat/src/autogen_agentchat/agents/_assistant_agent.py#L2372-L2438)

**对 Profile 的启示：**

- 可持久化 Profile 引用 `componentType + componentConfig`，不把 delegate、闭包、
  host client 实例直接塞进 YAML。
- `maxToolIterations`、并行工具策略、structured output 和 reflection 应显式
  配置并有默认值，避免由模型或宿主隐式决定。
- 运行态 state、message history、usage 和 trace 属于 session/run 记录，不属于
  profile identity。

## 结合当前 UniClaw 的边界建议

当前 `src/UniClaw.Agent/` 已拥有 Primary Goal、Execution Contract authoring 和
Goal Evaluation 的产品语义；README 明确 UniAgent 不拥有 Current WorldBelief、
Run State、Effect judgment，也不直接产生现实动作授权。`src/UniClaw.Agent.Dsh/`
已经承担 DSH realization/profile/model adapter。由此建议把 Profile 放在
**realization 装配层**，由 `UniClaw.Agent` 消费一个窄的只读 profile view，而不让
Profile 进入 Goal/Evaluation 的 authority 链。

建议的概念分层如下：

| 层 | Profile 可声明 | Profile 不应声明 |
|---|---|---|
| 身份与提示 | `profileId`、`schemaVersion`、agent name/description、`instructionsRef`、prompt version | Primary Goal 的事实、用户长期记忆正文 |
| 模型 | logical model role、provider adapter、model id、temperature/timeout、fallback policy | “模型能力=Authority”、直接决定 Kernel effect |
| 产品工具 | UniAgent tool vocabulary、capabilityRef、schema version、toolChoice、iteration/parallel limits | 直接调用设备/Effect provider、绕过 Kernel 的执行路径 |
| 上下文与记忆 | session/context/memory adapter ref、scope、retrieval limits、retention class | 把 Memory recall 当作当前 Evidence 或 Runtime truth |
| 编排 | handoff/agent-as-tool 引用、输入输出 contract | 第二套 Goal/Run lifecycle、跨层状态写入 |
| 安全与观测 | input/output guardrail refs、approval class、strict schema、timeouts、trace policy | 用 prompt 软约束替代 policy/effect gate |

一个可落地的 Profile v0.1 形状（仅设计草案，不是实现授权）可以是：

```yaml
schemaVersion: "uniagent.profile/v0.1"
profileId: "uniagent.dsh.default"
profileRevision: 1
identity:
  name: "UniAgent"
  description: "UniClaw cognition realization"
  instructionsRef: "prompt://uniagent/default@1"
model:
  role: "agent-decision"
  adapterRef: "dsh.model-management"
  logicalProfile: "agent-decision"
  settings: { temperature: 0, timeoutMs: 30000 }
tools:
  - toolRef: "agent.inspect"
    capabilityRef: "capability.inspect"
    schemaVersion: "1"
    execution: "kernel-mediated"
    approval: "policy-required"
    toolChoice: "auto"
limits: { maxToolIterations: 4, maxParallelTools: 1 }
context:
  sessionAdapterRef: "dsh.session"
  memoryAdapterRef: "memory.service"
  memoryScope: "product-session"
security:
  inputGuardrails: ["policy.input.default"]
  outputGuardrails: ["policy.output.contract"]
  failClosedOn: ["unknown-tool", "schema-mismatch", "missing-evidence"]
observability:
  tracePolicy: "product-run-correlated"
```

这个形状的关键是 `execution: kernel-mediated`、`capabilityRef` 和
`failClosedOn`：它把 DSH/模型/工具接入描述为可替换 realization，同时保留
UniAgent→Kernel 的既有 authority 边界。`profileRevision` 解决同一 schema 下的
可审计变更；`instructionsRef`、tool schema version 和 adapterRef 让一次 run 能
重建实际装配，而不把运行态对象持久化。

## 尚未决定的项目问题

1. Profile 是每个 Product Session 固定，还是允许在新 Run 边界切换 revision；应
   先定义 revision pinning 和 terminal run 后不可变规则。
2. `memoryScope` 的 buyer 是 Memory System 还是 Product Session；在 Memory
   System 的 canonical schema 未冻结前，Profile 只能声明 adapter/ref，不能定义
   记忆记录格式。
3. `capabilityRef` 与产品 tool vocabulary 的映射由哪个 registry 维护；应延续
   当前 CapabilityHub/adapter 单向映射，避免在 Profile 复制 capability schema。
4. DSH 的现有 profile（deployment/model/capability allowlist）与 UniAgent Profile
   的关系需要明确为“宿主 deployment profile + 产品 realization profile”，不能
   让 DSH profile 反向成为 UniAgent 产品 authority。

