# UniClaw Product Context 与 Prompt 管理梳理

> 日期：2026-10-07
> 状态：设计梳理，未授权实现
> Status: DRAFT（设计梳理，未授权实现）
> Authority: `NONE`（分析梳理，不是架构决策；与 ADR / baseline 冲突时以后者为准）

## 核心判断

UniAgent 看到的内容不应该由一个“总 Prompt”管理。当前产品至少有六种不同性质的输入：产品指令、安全策略、任务契约、当前运行上下文、历史知识、实时 Evidence。它们的 owner、生命周期、可信度和是否能影响动作都不同。

管理原则是：

- **规则放在 Policy/Contract，由 Kernel 执法**；
- **稳定身份和输出纪律放在 Product Prompt**；
- **当前任务和当前状态由 Kernel 每轮生成**；
- **历史知识由 Memory System 按需召回**；
- **当前现实由 Perception/Evidence 确认**；
- **DSH 只负责 realization、profile 组合和 transport**。

## 六类输入的管理表

| 内容 | 语义 owner | 存储/来源 | 加载时机 | 能否单独授权动作 |
|---|---|---|---|---|
| Product Prompt | UniAgent/Product realization | 版本化产品插件或 prompt manifest | Agent scope 创建时 | 不能 |
| Safety Policy | Kernel/Effect Boundary | 版本化策略文件 + 机械校验 | Run/Contract 初始化与每次 dispatch 前 | 能约束/拒绝，不能扩大权限 |
| Execution Contract | UniAgent + Kernel | Product Session/Run canonical state | Run 初始化 | 规定允许/禁止 effect 和证明条件 |
| Runtime Context | Kernel/World Model/Capability | 当前 Run 的派生 view | 每次 consult | 不能；只是当前输入 |
| Memory Recall | Memory System | durable records + provenance | 任务开始或未知/失败时按需召回 | 不能；只能作为 prior/context/hypothesis |
| Current Evidence | Perception/Evidence Ledger | 当前 observation artifact 和 accepted Evidence | 动作前、动作后和验证阶段 | 可支持当前事实判断，但不直接替代 Guard |

## 管理平面

### Authority Plane

这里放不允许由模型改写的内容：

- capability allowlist；
- forbidden effect；
- dangerous target taxonomy；
- permission 和 escalation 规则；
- Contract 的 allowed/forbidden effects；
- Completion/Verification proof criteria。

它们必须进入 Kernel/Effect Guard 的输入。Prompt 只能解释规则，Memory 只能提供历史参考。

### Product Instruction Plane

这里放 UniAgent 的稳定身份和行为形状：

- 你是 UniClaw Product Runtime 的 UniAgent；
- 只能通过指定产品工具输出；
- 不能把 Memory 当作当前事实；
- 遇到未知、冲突或危险目标必须停止或请求观察；
- 输出必须符合 Product protocol。

这部分应该由产品专用 prompt plugin 在 `uniagent-prod` scope 注册，不应从开发仓库的 `AGENTS.md` 或 coding Skills 继承。

### Runtime Context Plane

这里放每轮变化的最小上下文：

```text
ProductSessionId / RunId / DecisionId
Objective / phase / budget
AllowedEffects / availableCapabilities
Current Screen / Elements / Claims
PendingObligations / Progress
FailureReason / FailedStepIndex
```

它由 Kernel 派生，DSH adapter 只负责传输。不能让 DSH session history 成为 Runtime truth，也不能把整个 World Model 倾倒给模型。

### Knowledge Plane

这里包含两个不同的来源：

- **Normative Catalog**：官方文档和平台 API 提炼出的机制知识，例如 Android UI Automator、iOS XCUI、Windows UI Automation、WebDriver 的能力和限制；按来源、版本和适用平台管理。
- **Strategy Memory**：真实运行后沉淀的入口先验、selector 候选、失败模式和恢复策略；必须带适用范围、时间、provenance、失效条件和冲突关系。

两者都只能影响观察和规划，不能直接放行动作。

## 三个时间点

### Profile Boot

只做一次：

1. 读取并校验 Product Prompt manifest；
2. 注册 `uniagent-prod` scope 的静态产品提示词；
3. 注册冻结的 Product tool manifest；
4. 关闭或隔离开发 Harness 的 `AGENTS.md`、Skills 和 runtime context；
5. 记录 prompt revision、policy revision、protocol schema hash。

### Product Run Initialization

由 Kernel 完成：

1. 建立 Product Session/Run；
2. 生成 Execution Contract；
3. 确定 allowed/forbidden effects；
4. 读取当前 DeviceProfile 和 capability profile；
5. 绑定真实 DSH session；
6. 写入首个可审计的 initialization envelope。

### Consultation Turn

每轮由 Kernel 生成 `AgentDecisionContext`，再由 DSH adapter 组合：

```text
静态 Product Prompt
+ 当前 AgentDecisionContext
+ 可选 MemoryRecall
+ 可选 screenshot
→ DSH model turn
→ submit_decision
```

固定规则不要每轮重复生成；当前状态、预算、失败原因和允许的 effect 必须每轮更新。

## 文件和配置的建议归属

```text
.dsh/
├── profiles/                  # DSH/Host 运行 profile
├── profile-adapter/           # DSH → Product 绑定
└── model-bindings.yaml        # realization 的模型绑定

product/
├── prompt/                    # 产品静态 prompt manifest 与版本
├── policy/                    # 安全策略、危险动作分类、allowlist
└── knowledge/                 # 官方文档抽取后的 Normative Catalog

Memory System                 # durable Strategy Memory 与 provenance
Evidence/Run owner            # 当前事实、原始 artifacts、post-action evidence
```

具体目录名可以调整，但 owner 不应改变。`.dsh/` 不应成为 Product Policy 或 Memory 的 canonical source；DSH 只消费产品侧已发布的绑定和 prompt/policy 投影。

## 版本与审计

每次 Product consult 至少要能追溯：

```text
protocolSchemaHash
productPromptRevision
safetyPolicyRevision
contractVersion
runtimeProfileRevision
memoryRecallIds（若有）
evidenceRefs
model/provider route
```

Prompt revision 改变时，应产生新的 profile/prompt revision；Policy revision 改变时，应阻止旧 Contract 静默继续使用。Memory record 更新应通过 supersede/expire 留下历史链，而不是原地覆盖造成不可解释的结果。

## 当前 profile 的问题

当前 `~/.dsh/profiles/web/cordis.patch.yml` 已有 `uniagent-prod` preset、`submit_decision` 和 `allow: []` 工具收敛；但：

- `uniagent-prod` 没有产品专用静态 prompt plugin；
- DSH base 仍挂载 `agent-instructions`，可能读取 `~/.dsh/AGENTS.md` 和仓库 `AGENTS.md`；
- 当前产品协议提示词主要在 `dsh/uniclaw-decision-channel/src/index.js` 的 `consultationPrompt()` 中按轮次生成；
- Memory MCP 当前未启用；
- Settings forbidden-action policy 目前是 Runner fixture，不是 Product Policy 的统一 owner。

## 建议的最小整理顺序

1. 先冻结 Product Prompt、Safety Policy、Runtime Context、Memory Recall 四个概念和 owner。
2. 为 `uniagent-prod` 增加产品静态 prompt 的独立 scoped seam，并隔离开发 `AGENTS.md`/Skills。
3. 把危险动作策略从 Settings fixture 升格为可版本化、可机械执行的 Product/Kernel policy 输入。
4. 保留 `consultationPrompt()` 作为每轮动态上下文和协议纪律的组合点，避免把当前状态写入静态 prompt。
5. 最后接入 Memory；Memory 只提供带 provenance 的 recall，不改变 Tool allowlist、Contract 或 Guard。
