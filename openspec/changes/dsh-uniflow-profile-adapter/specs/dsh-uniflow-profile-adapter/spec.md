# Spec: dsh-uniflow-profile-adapter

> DSH 侧 UniClaw Profile Core 消费适配器。WHAT-only；实现见 `tools/dsh_profile_adapter.py`。
> 上游权威：`.ai/profiles/*.json`、`.ai/schemas/work-{item,result}.schema.json`、
> `tools/agent_profile_validator.py`、`.ai/workflows/codex-coding-workflow.md`。

## ADDED Requirements

### Requirement: Profile Source 校验后才可激活工作流

DSH MUST 通过 `profile_source` 配置（root / schema_version / source_revision /
validation_command / mode）加载上游 Profile。激活时 MUST 执行 validation_command
并校验 schema_version 与 source_revision；任一不匹配或验证失败时 MUST 拒绝启动
工作流激活。DSH MUST NOT 写入 Profile Source 路径下任何文件。

#### Scenario: 验证通过后可加载

Given 上游 `validate` 输出 PASS 且 revision 匹配，
When 加载 Profile Source，
Then 注册表加载成功并记录 `profile.source.validated` 与 `profile.loaded` 事件。

#### Scenario: schema 版本不匹配拒绝启动

Given 配置 `schema_version: 9` 与上游 `schema_version: 1` 不一致，
When 激活工作流，
Then 返回错误并拒绝启动。

#### Scenario: 上游验证失败拒绝激活

Given validation_command 退出码非 0，
When 激活工作流，
Then 返回错误并拒绝激活。

#### Scenario: 不修改上游 Profile

Given 任一适配器操作，
Then Profile Source 根目录下文件内容与 mtime 保持不变。

### Requirement: Profile 合并与冲突语义与上游一致

DSH MUST 委托上游 validator 的 `compose_profile` / `merge_mapping_strict` 语义
组合 `RoleProfile + ExecutionProfile + Optional ModuleProfile`，MUST NOT 自行
重新实现合并。规则冲突时 MUST 返回 `LeaderDecisionRequired` 并携带冲突规则 ID
与来源引用，MUST NOT 静默覆盖。

#### Scenario: 合并结果与上游一致

Given 相同 role/execution/module 输入，
Then DSH 组合结果与 `compose_profile` 输出逐字段一致。

#### Scenario: 冲突不被静默覆盖

Given 两层 Profile 在同一路径产生不同标量值，
When 组合，
Then 抛出携带路径与来源的 `LeaderDecisionRequired`，并记录 `profile.conflict`。

### Requirement: 模型绑定与 Profile 解耦且 Leader 权威唯一

Model Binding MUST 由 DSH 侧 `model_bindings` 配置独立维护：
`decision_frontier.primary` = zai/glm-5.2/high，`decision_frontier.fallback` =
opencode-go/glm-5.2/high，`implementation_efficient` 与 `semantic_read` =
opencode-go/deepseek-v4-flash，reasoning 统一默认 `high`（与 Host 侧
`agent-default-model.reasoningEffort: high` 对齐）。`tool_only` 绑定 `model: none`
且 MUST NOT 调用模型。
绑定中的 provider 名以 DSH Host 实际注册的路由为准（本部署注册 `opencode-go`；
`opencode` 未注册，派发以 `NO_ADAPTER` fail-closed）。primary 与 fallback MUST NOT
同时持有 Leader authority；fallback 仅在 provider 不可用、连接失败、超时、平台级
工具失败或连续结构化输出失败时接管，且接管后 MUST 从最新 LeaderCheckpoint 恢复。
业务失败（测试失败、决策错误、规则冲突、目标变化、拆分不合理）MUST NOT 触发
fallback。修改模型配置 MUST NOT 改变 RoleProfile 权限。

#### Scenario: 绑定正确

Then decision_frontier.primary 为 zai glm-5.2，fallback 为 opencode-go glm-5.2，
worker 为 opencode-go deepseek-v4-flash，tool_only 为 none。

#### Scenario: Leader authority 唯一

Given fallback 处于待命状态，
Then 同一时刻只有一个端点持有 `leader_authority: true`。

#### Scenario: 业务失败不触发 fallback

Given `worker_test_failed` 类失败原因，
When 请求 fallback，
Then 被拒绝并保留原 primary。

#### Scenario: fallback 从 checkpoint 恢复

Given primary 不可用且 checkpoint 已存在，
When fallback 接管，
Then 加载固定 Leader Profile、最新 checkpoint、pending work 与上下文引用，并记录
`leader.fallback.started`。

### Requirement: WorkItem 单播与 Worker 边界

每个 WorkItem MUST 恰好有一个标量 `worker_owner`；同一 WorkItem 的 fanout MUST
被拒绝；Worker MUST NOT 创建 SubAgent 或再委派；跨 WorkItem 的并发写 MUST NOT
覆盖同一文件；有依赖的 WorkItem 按序调度；write-heavy 默认串行。

#### Scenario: fanout 被拒绝

Given 同一 WorkItem id 出现第二个 owner，
When 校验 ChangeSet，
Then 报错并列出冲突 owner。

#### Scenario: Worker 创建 SubAgent 被拒绝

Given Worker 会话请求 spawn，
Then 拒绝（ExecutionProfile `spawn_agent: false` + 适配器硬校验双保险）。

#### Scenario: 同文件并发写被拒绝

Given 两个不同 owner 的 WorkItem 写同一路径，
When 校验 ChangeSet，
Then 报错（复用上游 `validate_change_set`）。

### Requirement: ModuleContext 自动加载与缓存失效

收到 WorkItem 后，DSH MUST 自动组合 module-worker RoleProfile + 当前
ExecutionProfile + 指定 ModuleProfile + validator `context` manifest（effective
AGENTS、entrypoints、contracts、test gates、ProfileContextKey），Leader 只需传
objective / principles / contract refs / acceptance / forbidden / escalation。
缓存键 MUST 为上游 `profile_context_key` 语义；Profile 版本、RuleDigest、
source revision、ModuleProfile、ModelBinding 任一变化，或 Worker
blocked/协议违规时 MUST 使缓存失效。ModuleContext 权威状态 MUST 由 DSH 持有，
模型会话仅作缓存。

#### Scenario: 上下文自动加载

Given 合法 WorkItem，
When 解析，
Then manifest 含 role/execution/module id、effective_agents、entrypoints、
test_gates、profile_context_key。

#### Scenario: ModuleProfile 唯一解析

Given 路径只属于一个模块，
Then 唯一解析该模块；归属不明确（0 或 >1 匹配）时返回 coding-leader。

#### Scenario: 缓存键变化即失效

Given module 版本或 rule digest 变化，
Then ProfileContextKey 变化，缓存条目不命中。

#### Scenario: 复用只追加授权内容

Given 同键重复任务，
Then 复用会话仅追加新 WorkItem、新 ChangeSet Contract 与已接受 delta。

### Requirement: WorkResult 接收门与 ModuleContext Delta

Leader MUST 依序执行接收门：WorkResult Schema → Profile 版本 → base revision →
Worker Owner → 实际修改文件 → write scope → local rules → invariant →
forbidden → 测试与证据 → scenario gate → Accept/Reject。Worker 自然语言结论
MUST NOT 替代 diff 与测试证据。只有 Leader 接受的 `module_context_delta` 才更新
DSH ModuleContext；未接受的 delta MUST NOT 生效。

#### Scenario: scope 外写入被拒绝

Given Worker 实际修改路径不在 `scope.write` 内，
Then Reject，理由 `write_scope_violation`。

#### Scenario: delta 生效受 Accept 控制

Given delta 存在但 Leader 未接受，
Then ModuleContext 不变；接受后按受控字段白名单更新。

### Requirement: LeaderCheckpoint 与最小事件

LeaderCheckpoint MUST 仅存储引用与决策摘要（frozen decisions、invariants、
contracts、completed/pending/blocked work、module_context_refs、evidence_refs、
active_leader_provider），MUST NOT 存储完整推理或完整 Worker 对话。运行事件
MUST 限制为：`profile.source.validated`、`profile.loaded`、`profile.conflict`、
`workflow.route.selected`、`work_item.dispatched`、`worker.context.loaded`、
`worker.completed`、`worker.blocked`、`work_result.accepted`、
`work_result.rejected`、`leader.fallback.started`、`checkpoint.updated`。

#### Scenario: checkpoint 更新

Given 工作被接受，
Then checkpoint pending→completed 迁移并记录 `checkpoint.updated`。

### Requirement: DSH Work Envelope 不改变通用语义

DSH 运行字段（protocol_version / session_id / run_id / correlation_id /
profile_version / work_item）MUST 只存在于外层 `dsh_work_envelope`，内嵌
WorkItem MUST 保持上游 schema 校验通过且不被污染。

#### Scenario: envelope 包裹不改语义

Given envelope 包裹的 WorkItem，
Then 解包后与原始 WorkItem 深度相等，且独立通过上游 `validate_work_item`。

### Requirement: 路由策略与 tool-only 不调用模型

路由决策 MUST 与 `.ai/workflows/codex-coding-workflow.md` §7 一致：确定性操作
→ tool-only（不调用模型）；单模块原子实现 → module-worker；测试编写 →
test-author；验证 → verifier；语义分析 → semantic-analyzer；公共接口 /
生命周期 / Invariant / 强耦合跨模块 → coding-leader（不提前派发）。

#### Scenario: tool-only 不调用模型

Given 确定性任务 shape，
When 路由，
Then 返回 tool-only 且绑定 model none，零模型调用计数。

#### Scenario: 强耦合跨模块不提前派发

Given cross_module 且 contract 未冻结，
When 路由，
Then 返回 coding-leader，不生成 WorkItem。

### Requirement: ExecutionProfile 权限门由上游裁决

development / test-authoring / verification / semantic-analysis 的写入边界
MUST 完全由上游 `validate_work_item` 裁决（含 test-authoring 不得写生产代码、
verification/semantic-analysis 不得写源码）；DSH MUST NOT 放宽。

#### Scenario: 各执行权限正确

Then development 可写 owned+test paths；test-authoring 仅 test paths；
verification 与 semantic-analysis 禁止任何 source write scope。

### Requirement: 强制 WorkItem 派发门

DSH MUST 以 `DshWorkflowRuntime.dispatch_work_item()` 为 UniFlow 唯一合法派发
入口。派发输入 MUST 是符合 `.ai/schemas/work-item.schema.json` 的 JSON 对象；
Markdown 标题、自然语言任务说明、缺失必填字段或非对象 `semantic_brief` 的输入
MUST 被拒绝（`WorkItemRequired`）。派发前 MUST 执行：Schema 校验、Profile 校验、
单一 `worker_owner` 校验、`scope.write` 权限校验、`leader_decisions_frozen=true`、
`unresolved_architecture=[]`、以及 ExecutionProfile 与任务形态一致性校验。
`tool-only` WorkItem MUST 满足：不创建 Subagent、model=`none`、不声明源码或
测试写入范围、不请求语义判断；违反任一条 MUST fail-closed。

#### Scenario: Markdown 任务说明不能派发

Given 输入为 `# 任务：…` 文本，
When 调用 `dispatch_work_item`，
Then 抛 `WorkItemRequired`，不产生 dispatch 记录。

#### Scenario: tool-only 源码写入 fail-closed

Given `execution_profile=tool-only` 且 `scope.write` 非空，
When 校验派发，
Then 拒绝（"source or test write scope"），不创建 Subagent，零模型调用。

#### Scenario: ExecutionProfile 与任务形态一致

Given WorkItem 声明 `development` 而 task_shape 路由到其他 ExecutionProfile，
Then 派发被拒绝。

### Requirement: DispatchGate 解析 ModelBinding 进入 DSH Envelope

`dispatch_work_item()` MUST 按 `.dsh/profile-adapter/profile-source.yaml` 的
`model_bindings` 解析 ExecutionProfile → binding role：`development` /
`test-authoring` / `verification` → `implementation_efficient`；`semantic-analysis`
→ `semantic_read`；`tool-only` → `tool_only`（model none）。解析出的运行元数据
MUST 写入外层 `dsh_work_envelope.model_binding`，至少包含 binding role、provider、
model、reasoning、profile version、binding config revision/digest、work_item id、
worker_owner；MUST NOT 写入通用 WorkItem。实际值 MUST 来自 DSH 绑定配置，不得在
RoleProfile / ExecutionProfile / WorkItem / Worker 提示词中硬编码。

#### Scenario: Envelope 含完整 requested binding

Given 合法 `development` WorkItem，
When 派发，
Then `dsh_work_envelope.model_binding` 含 provider=opencode-go、
model=deepseek-v4-flash、reasoning=high 及 revision/digest/id/owner，
且解包后的通用 WorkItem 不含这些字段并仍通过上游校验。

### Requirement: Host 派发与能力 fail-closed

DSH Host seam（`DshHostClient`）MUST 从已校验 Envelope 读取
provider/model/reasoning 并显式传入 Subagent 创建；MUST NOT 使用会话默认模型、
自动模型或 Worker 自述模型替代。Host 不支持指定 provider/model/reasoning 时，
MUST 在任何文件修改或调度记录产生前抛 `RoutingCapabilityRequired`（code
`ROUTING_CAPABILITY_LIMIT`）。`tool-only` MUST NOT 走 Host seam。仓库不存在
可控制的 Host seam 或 Host 无法提供模型回执时，MUST 如实汇报，不得用模拟代码
宣称已保证实际模型。

#### Scenario: Host 能力不足写入前 fail-closed

Given Host 不支持 requested binding，
When 派发，
Then 抛 `ROUTING_CAPABILITY_LIMIT`，Host `.spawn_calls` 为空，无 dispatch 记录。

### Requirement: 实际模型回执与 WorkResultGate 核对

Subagent 启动后 DSH Host MUST 生成不可由模型正文替代的运行回执，至少包含：
session/subagent id、work_item id、worker_owner、actual provider、actual model、
actual reasoning、binding config revision/digest、started_at。模型文本自述
（如“我是 deepseek-v4-flash”）MUST NOT 视为回执。回执进入 DSH 运行元数据 /
事件日志。Worker 写文件前 MUST 完成预执行回执核对；WorkResultGate 接收结果时
MUST 再次核对 requested binding 与 actual receipt 一致、work_item id 一致、
worker_owner 一致、profile/binding revision 一致。缺少回执或任一字段不一致时
MUST：拒绝结果、不接受 ModuleContext Delta、记录 `model_receipt_missing` /
`model_binding_mismatch`、返回 `ROUTING_CAPABILITY_LIMIT`，MUST NOT 静默 fallback。

#### Scenario: 合法回执与合法 WorkResult 通过

Given Host 回执 actual provider/model/reasoning 与 requested binding 一致，
When 接收结果，
Then 全部 Gate 通过，接受结果并应用 ModuleContext Delta。

#### Scenario: 缺少回执拒绝结果

Given WorkResult 无 Host 回执，
When WorkResultGate 接收，
Then 拒绝，理由 `model_receipt_missing`，不应用 Delta。

#### Scenario: 回执不符拒绝结果

Given actual provider/model 与 requested 不一致，
When WorkResultGate 接收，
Then 拒绝，理由 `model_binding_mismatch`，不应用 Delta，不静默 fallback。

### Requirement: Leader 绑定与唯一 authority

UniFlow 启动时 MUST 记录 Host 提供的 Leader 实际模型回执；主 Leader 绑定 MUST
为 zai/glm-5.2/high（`assert_leader_primary`）。只有
`profile-source.yaml` 允许的平台级原因（provider_unavailable / connection_failure
/ timeout / platform_tool_failure / structured_output_repeated_failure）才可触发
Leader fallback；fallback 接管 MUST 撤销原 endpoint 的 leader_authority、为新
endpoint 授予唯一 leader_authority、从最新 LeaderCheckpoint 恢复并记录实际 Host
回执。业务失败 / 测试失败 / 规则冲突 / Worker 失败 MUST NOT 触发 Leader fallback。

#### Scenario: Leader primary 绑定正确

Then `assert_leader_primary()` 返回空列表；primary 为 zai/glm-5.2/high 且持有
唯一 leader_authority。

#### Scenario: 真实 Host 回执与 primary 不一致时 fail-closed

Given 当前 UniFlow 会话 Host 实际回执为 opencode-go/deepseek-v4-flash，
When 记录 Leader 回执并与 primary 比对，
Then 判定 `model_binding_mismatch`，不静默降级；需要 Host 以 zai/glm-5.2
启动会话或经允许的平台级原因触发唯一 authority 交接。
