# dsh-uniflow-agent-loop — Plugin Design (Decision Record)

> 状态: DECIDED (2026-08-25) — 方案已定，实施待排期
> 决策类型: DSH Runtime Extension Plugin + Tool Plugin
> 关联: `.dsh/profile-adapter/*`（配置真相源）、`tools/dsh_profile_adapter.py`（离线校验/当前实现）

## 结论

将 UniFlow（合法 WorkItem → 派发 → 显式模型绑定 → 真实回执 → 验收）从"会话内
手工流程 + Python 脚本"升级为 DSH Host 内**机械强制插件** `dsh-uniflow-agent-loop`，
解决三个现实缺口：

1. 模型绑定靠"执行者记得读 Envelope"→ 插件在 spawn 时强制；
2. reasoning 无法经 `agentOptions` 传入 → 插件用 `installModelSelection` 注入；
3. 回执靠事后翻会话日志 → 插件实时订阅 `request/header` 生成回执。

## 锚点（已核实的 harness Hook）

| Hook | 官方位置 | 用途 |
|---|---|---|
| `ctx.subagents.registerProvider(name, provider)` | `packages/subagent/subagent/src/index.ts` | 注册 `uniflow` provider：`start()` 强制 binding |
| `installModelSelection(agentCtx, selection)` | `packages/core/agent/src/model-selection.ts` | 注入 provider/model/reasoningEffort，经 `agent/request` 应用 |
| 子会话 `request/header` / `subagent/descriptor` | agent-loop 写 session | 真实 Host 回执（模型自述不可伪造） |
| `ctx.tools.register(defineTool(...))` | `packages/subagent/tool-subagent/src/index.ts` | `uniflow_dispatch` / `uniflow_accept` 工具 |
| `settingsNamespace('uniflow')` | cordis settings | 插件配置（指向 profile-source.yaml） |

## 架构

```text
dsh-uniflow-agent-loop (cordis 插件; inject: tools, subagents, session, settings, systemPrompt)
├─ 工具层
│  ├─ uniflow_dispatch(work_item) → Envelope + requested binding
│  └─ uniflow_accept(envelope, result) → ResultGate → delta
├─ Loop 强制层
│  └─ registerProvider('uniflow')
│     start() → 读 envelope.model_binding
│             → agentOptions {provider, model}
│             → installModelSelection(reasoningEffort)
│             能力不足 → start 前 ROUTING_CAPABILITY_LIMIT
└─ 回执层
   └─ 订阅子会话 request/header → HostReceipt
      {session, work_item_id, owner, actual provider/model/reasoning, verdict}
```

## 组件与职责

| 组件 | 机制 | 职责 |
|---|---|---|
| `WorkItemGate` | 工具调用内纯函数 | schema/必填/单 owner/冻结/tool-only 无写入 |
| `BindingResolver` | 读配置 | ExecutionProfile → `model_bindings`，产出 requested + digest |
| `SpawnSeam` | `registerProvider('uniflow')` | 从 Envelope 强制 provider/model；写前 `ROUTING_CAPABILITY_LIMIT` |
| `ReasoningInjector` | `installModelSelection` | 把 requested reasoningEffort 应用到子 agent 每步请求 |
| `ReceiptListener` | 订阅子会话 `request/header` | Host 实际回执 |
| `ResultGate` | `uniflow_accept` 工具 | requested vs actual + id + owner + revision；缺/不一致拒绝且不应用 delta |
| `Config` | `settingsNamespace('uniflow')` | 指向 profile-source.yaml（可覆盖） |

## 关键流程

### 派发（uniflow_dispatch）
```text
输入: work_item(JSON)
→ WorkItemGate（复用 .ai/schemas/work-item.schema.json）
→ BindingResolver: execution_profile → {provider, model, reasoning, digest}
→ Envelope: {work_item, model_binding, profile_version, run_id}
  tool-only: model=none, 不 spawn
```

### Host spawn（核心）
```text
registerProvider('uniflow', provider)
  start(request):
    env = session.uniflow[parentSessionId].envelope      # dispatch 时登记
    if !env.model_binding: throw WorkItemRequired
    if !llm.canRoute(provider, model): throw ROUTING_CAPABILITY_LIMIT  # 写入前
    child = 真实 spawn（复用内建 spawn provider 路径）
    installModelSelection(child.ctx, {
      provider, model,
      reasoningEffort: env.model_binding.reasoning,      # 补齐当前缺口
    })
```

### 回执
```text
订阅子会话 'request/header' → config.provider/model/reasoningEffort
→ HostReceipt（work_item_id/owner 由 envelope 登记关联）
→ session 级 uniflow.receipts[id]
```

### 验收（uniflow_accept）
```text
输入: envelope + result
→ ResultGate: schema → revision → owner → scope → evidence → receipt
   receipt 缺/不一致 → {rejected, code: ROUTING_CAPABILITY_LIMIT}, delta 不接受
   reasoning 缺失 → 用 Host 默认（agent-default-model）补齐（对齐现有 Python 行为）
→ 全过 → 接受 Result + ModuleContext delta
```

## 与现有仓库的关系

- `.ai/`、`.ai/schemas`、`profile-source.yaml`、Python validator = 配置与离线校验真相源；
  插件读取它们（或等价实现），不建第二套真相。
- `tools/dsh_profile_adapter.py` → 保留为 CLI/校验/测试；运行时闭环迁至插件（渐进）。

## 实施里程碑

| M | 内容 | 验收 |
|---|---|---|
| M1 | 包骨架 + DispatchGate + BindingResolver（纯函数） | 20+ 用例，复用现有 schema |
| M2 | `registerProvider('uniflow')` + 能力 fail-closed | spawn 强制绑定；写前 ROUTING_CAPABILITY_LIMIT |
| M3 | `installModelSelection` reasoning 注入 | 子请求 header 含 reasoningEffort，回执含 actual reasoning |
| M4 | 回执监听 + uniflow_accept | 缺/不一致回执拒绝；合法通过 + delta |
| M5 | 集成：真实 Host 只读 subagent 走完闭环 | actual==requested；既有 Python 测试全绿 |
| M6 | README + 安全声明 + 挂载示例 | 符合 awesome-dsh-plugin 收录格式 |

## 边界与安全

- 不替代 Agent 决策：插件只提供 gate/证据/能力；
- 不保存 Runtime 真相：回执为会话级视图；
- 权限透明：不读敏感目录、不自动外传；
- Hook 明确：只挂 subagent/start、agent/request、子会话 request/header；
- 卸载安全：`ctx.effect` dispose 全部注册。