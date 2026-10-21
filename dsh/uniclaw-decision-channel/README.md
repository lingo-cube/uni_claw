# `@uniclaw/dsh-decision-channel`

UniClaw Product 的 DSH 决策通道。它创建任务级 DSH session，并把会话归入可配置的 DSH 项目，同时为 Product 和 Slow 对话写入可配置标题。

## 配置

配置写在 DSH web profile 的宿主插件行，不写在插件源码中：

```yaml
- id: uniclaw-decision-channel
  name: '@uniclaw/dsh-decision-channel'
  config:
    workspaceTitle: UniClaw Product Tasks
    workspaceKey: UniClaw Product Tasks
    workspaceReuse: true
    sessionTitle: 遍历设置菜单
    slowSessionTitle: 遍历设置菜单 · Slow
    autoCloseTurn: false
```

| 字段 | 作用 | 缺省值 |
|---|---|---|
| `workspaceTitle` | DSH 侧边栏中的项目名 | `UniClaw Product Sessions` |
| `workspaceKey` | 共享项目目录键；同一键在不同 Product task 间复用项目 | `workspaceTitle` |
| `workspaceReuse` | 是否按 `workspaceKey` 复用测试项目 | `false` |
| `sessionTitle` | 任务级 Product 对话标题 | `UniClaw Product Consultation` |
| `slowSessionTitle` | 临时 Slow 对话标题 | `${sessionTitle} · Slow` |
| `autoCloseTurn` | 成功提交后是否主动 cancel 当前物理 DSH turn | `false` |
| `promptMount` | 静态产品 prompt 的挂载方式（PRF-003）：`section`=preset 行注册 scoped system-prompt section，每轮只带动态内容；`per-turn`=每轮携带全部静态文本（回退通道，宿主两行都要设，避免 section+每轮重复） | `section` |
| `skillArtifact` | boot 时加载的 package-relative Product Skill 目录或 `{path,name,revision,sha256}` 快照；校验通过后只挂载一次 scoped section，运行中不热切换 | `skill/android-automotive-ui-testing` |

标题和键字段必须是非空字符串，布尔字段必须是布尔值。插件通过 `workspaceRegistry.create` 和
`sessionController.rename` 写入 DSH 的正式持久化服务，不直接改 storage 文件。
Slow 每次使用独立 session，成功、失败、超时、中止和 detach 后通过
`workspaceRegistry.archiveSession(id, {stopActivity:true})` 归档并停止活动，保留
原始 transcript；宿主缺少归档接口则拒绝发起 Slow，归档失败显式返回诊断。
`workspaceReuse: true` 时，`workspaceKey` 对应的 canonical workspace path 会由
DSH 的 registry 幂等复用，多个测试 task 共用同一个项目；每个 task 仍创建自己的
Product session，并用 `sessionTitle` 命名。`autoCloseTurn` 默认关闭，避免共享
服务被 Product task 主动回收；只有显式设为 `true` 才会在工具提交后调用 cancel。

静态产品 prompt（身份/输出纪律/payload 形状）不再写在 `consultationPrompt()`
里：canonical 工件在仓库 `product/prompt/uniagent-prod/`（manifest.json 声明
`promptRevision` 与协议 schema 耦合哈希），包内 `prompt/` 副本必须与其逐字节
一致（同步测试执法）；改 prompt 必须经 `python3 tools/prompt-manifest-hash.py
product/prompt/uniagent-prod` 重算哈希并递增 `promptRevision`，schema 变更
未同步 bump manifest 会在插件加载时 fail-closed。

当 `AgentDecisionContext.budgetReview` 非空时，UniAgent须在当前五种 wire
decision之一中提交顶层 `reviewResponse`，回带 reviewId、显式 outcome、必填的
独立 assessment，以及仅在 RequestRenewal 时使用的 renewal。插件按生成 schema
严格校验该字段；Runtime仍是预算授予与事实记录的执法者。旧 proposal.renewal
与 defer 顶层 renewal 仅为既有记录兼容路径。

领域 Skill 的 canonical 工件在仓库 `product/skills/android-automotive-ui-testing/`，
包内副本必须逐字节同步。启动时校验 `schemaVersion`、可读 name、revision、
guidance、references 和 SHA-256，并以 `uniagent-prod:skill:<name>` 注册一次 scoped
section；`per-turn` 回退模式会把同一 guidance 与静态 Product Prompt 一起放入每轮内容。
Skill 只提供观察、规划、受控执行、验证和证据指导，不授予额外 Capability、Effect 或
Memory 权限。Profile allowlist 和 Runtime initialization envelope 会记录其
name、revision、sha256。

在创建 DSH session 前，`/api/uniclaw-agent/handshake` 还要求调用方发送
adapter 元数据 `expectedArtifacts`：已加载 Profile 的 Prompt `promptHash` 和单个
primary Skill 的 `{name, revision, sha256}`。插件把它们与进程启动时校验并加载的
实际工件比较；缺失、格式错误或任一指纹不符都会拒绝握手，不会创建 session 或
发起模型请求。接受响应带回实际 `artifacts` 指纹，C# peer 再次核对。该元数据只
属于 DSH adapter 握手，不扩展 Kernel AgentDecision 或产品协议 Schema。

握手使用 `systemPrompt.assemble({scope: actualAgent})` 核对真实会话输入；
section 模式缺少完整 Prompt 或 Skill 时拒绝握手。`GET /api/uniclaw-agent/prompt-probe`
返回该实际会话的输入名称清单，不返回正文；没有会话作用域的 `assemble({})`
只检查全局输入，不能作为 preset 挂载证明。

修改 profile patch 后，需要重新启动专用测试服务才会生效：

```bash
DSH_TEST_PORT=3081 dsh/test-service.sh
```

不要用 3080 做这项验证；`dsh/test-service.sh` 会拒绝 3080，并且不会停止已有进程。

## 验证

```bash
node --test dsh/uniclaw-decision-channel/tests/plugin.test.mjs
dsh/deploy.sh --check-only
```

插件只接受四类 Product 输出：`act`（有序计划步骤）、`policy`（策略规则）、
`noAction` 和 `defer`。每次成功咨询的回执都带有 `requestId`、`generation`、
`productRunId`、`decisionId`、`schemaHash`；策略额外带 `policyId`。因此可以从
一次咨询回到对应的 Product Run、DecisionId、schema 版本和策略，而不是只看到
一段无法关联的模型文本。`maxRounds` 与 `maxApplications` 必须为正数；缺字段、
越界值、错误 DecisionId 或额外字段都会 fail closed。插件测试覆盖四类成功输出、
边界拒绝、会话映射和回执关联。

`submit_decision` 拒绝时同时返回稳定的 `reason` 和具体字段 `message`，渲染内容
保留两者，供模型在同一次咨询中纠正。Act/NoAction 的续额字段位于
`task.payload.proposal.renewal`；Defer 位于 `task.payload.renewal`。错层字段仍严格
拒绝，不作隐式搬移或自动加额。

ADR-0053：正式通道新增plan与continuePlan，并同步既有blocked零Effect出口。Plan支持具体步骤、候选迭代、Owner命名条件的if分支、有界repeat及检查点；有限块可组合，不开放任意表达式/跳转/递归调用；typed结构由Kernel记录生成Schema，插件只验证/传输。ContinuePlan精确回带context.planExecution.position，续额不隐式续Plan；NoAction保持终局评估。Source及Condition声明仅首次请求注入，未来页面动作由Runtime逐步检查当前事实。实际attempt完成锚使用plan-attempt:ID，不把同节点下一次迭代误作上次完成。咨询落盘不是进程重启执行恢复承诺。

Agent咨询的attempt保留身份、位置、状态、receipt及原因，EvidenceRefs使用实际plan-attempt审计引用；完整原始来源仍留在Driver审计及plan-execution证据。引用存在不代表Verified，completion仍按内核原有状态核验。


WORLD-OBSERVATION-001：`/slow`可携带`reasoningEffort: "high"`。插件只在该隔离slow session的`agent/request` waterfall施加请求参数，不修改主决策session或默认effort。实际请求hook应用后响应`diagnostics.reasoningEffort=high`；未应用返回`reasoning-effort-unproven`，不静默降级。模型能力声明必须支持该档位，否则供应方拒绝；该参数不进入产品共享协议。
