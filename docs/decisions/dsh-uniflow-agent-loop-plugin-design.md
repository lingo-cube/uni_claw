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

---

## 生命周期与配置管理（2026-08-25 增补；DSH 实证核查）

> 增补动因：本机实证发现 `~/.dsh/profiles/web/package.json` 的
> `file:` 插件依赖指向已删除目录（ding-chime 悬空链接事故），
> 证明"安装形态 + 重启恢复"必须在设计内显式处理，不能依赖惯例。

### L1. 配置分位（单一真相，各归其位）

**形态决策（2026-08-25 定稿）：代码与包分离，以版本包供能。**
源码住 `~/Documents/Code/dsh-plugins/uniflow-agent-loop/`（独立 git 仓，
纯开发态）；经 `npm publish` 发布为自包含版本包 `dsh-uniflow-agent-loop`
（`files: [lib, README]`，零运行时依赖，host 部件以 peerDependencies 声明，
模式对齐 `dsh-mcp-manager`）；DSH 侧 `dsh plugin --profile web add
dsh-uniflow-agent-loop` 版本化安装 + `cordis.patch.yml` insert 挂载。

| 配置 | 位置 | 版本化 | 插件访问方式 |
|---|---|---|---|
| Profile 语义/WorkItem schema | repo `.ai/`（不变） | git | 只读 |
| 绑定与钉扎（revision/校验命令/模型绑定） | repo `.dsh/profile-adapter/profile-source.yaml`（不变） | git | 启动时读取 + digest |
| 插件源码 | `dsh-plugins/uniflow-agent-loop/`（独立仓，纯开发态） | git | — |
| 插件安装声明 | `~/.dsh/profiles/web/package.json`（**npm 版本范围，如 `^1.0.0`**）+ `cordis.patch.yml` insert | npm registry | 由 DSH profile 机制加载 |
| 插件运行设置 | `settingsNamespace('uniflow')` 仅存 profile-source.yaml 路径指针 | 随 profile | 读指针，不复制语义 |

**安装形态约束（ding-chime 教训，升级为结构保证）**：生产安装一律
npm 版本包（不可变、registry 托管，不依赖本地目录存活）；profile root
**禁止任何本地 `file:` 插件依赖**。源码→本地符号链接仅限开发回路
（对齐 dsh-mcp-manager README 的源码兜底说明）。Web 面 HMR 关闭，
挂载/升级后需重启 `dsh web`——生产语义一律冷启动（见 L2）。

**版本↔协议兼容**：包声明兼容的 `protocol_version`（当前 1）；启动自检
binding digest 与 repo 不一致 → `STALE_PROFILE_SOURCE` 拒载。升级/回滚
走 `dsh plugin add dsh-uniflow-agent-loop@x.y.z` + 重启，EventLog 记录版本切换。

**校验逻辑不进包**：schema/WorkItem 裁决保留在 uni_claw 侧
`tools/dsh_profile_adapter.py`（单一权威）；插件经 settings 指针调用
repo 侧 CLI，包内不携带第二套真相。

### L2. 重启语义（状态三层，各自回答）

| 状态层 | 位置 | DSH 重启后 |
|---|---|---|
| 静态配置 | repo（L1） | 无损（git + 盘上） |
| 进行中派发：envelope / 模型回执 | session 持久日志（`~/.dsh/sessions/*.jsonl`） | **可重建**：插件启动时经 `read_host_receipt_from_session_log` 扫描本 session 恢复回执；不可恢复 → 标记 `RECEIPT_LOST`，ResultGate 拒绝对应 delta（fail-closed，不猜、不静默放行） |
| ModuleContext / delta 状态 | repo `.dsh/profile-adapter/state/`（ModuleContextStore 盘上 JSON，现有实现） | 无损；**状态主权在 repo 侧，插件只是消费者**——插件卸载/损坏不丢失状态 |

热重载（`ctx.effect` + HMR）仅视为开发便利；生产语义一律按**冷启动**设计。

### L3. 插件不可用（缺失/崩溃/卸载）→ fail-closed 降级

- `uniflow health` 工具：插件 loaded 状态、profile-source validate 结果、
  绑定 digest、EventLog 概要——agent 与人可查；
- 插件缺失时 UniFlow **不退回自愿仪式路径**（否则重新打开 C5 失效模式）：
  派发请求显式失败 `ADAPTER_UNAVAILABLE`；
- 唯一 fallback 是 repo 侧 CLI（`dsh_profile_adapter.py dispatch/receipt`），
  且它同样原子产出 dispatch record——两条通道**同构可审计**；
- profile-source revision 漂移（repo 前进、绑定未更新）→ 启动即
  `STALE_PROFILE_SOURCE` 拒载，禁止降级运行。

### L4. 安装完整性的机械校验

`dsh_profile_adapter.py validate` 扩展：检查插件包在 profile root 可解析、
无悬空符号链接、settings 指针指向存在文件、绑定 digest 与 repo 一致。
每次冷启动插件自检三件（loaded / source valid / digest 一致）并写 EventLog。

### L5. 对里程碑的修订

| M | 增补验收 |
|---|---|
| M4 | + 重启恢复：杀掉 DSH 进程→重启→从 session 日志恢复回执；不可恢复路径产出 `RECEIPT_LOST` 拒绝 |
| M6 | + 安装完整性校验入 `validate`；ding-chime 式悬空链接被检出 |

## 实施进度（2026-08-25）

- **M0（repo 侧 CLI 收口）已完成**：`dsh_profile_adapter.py dispatch/receipt`
  子命令；dispatch record 原子落盘（`os.replace`）；`DeferredSessionSpawnHostClient`
  延迟 spawn 语义（PENDING 回执过不了 WorkResultGate，验收前必须 `receipt`）；
  13 用例 + 116 回归全绿；实弹核对当前 session 返回 `RECEIPT_MISMATCH`
  （实际 glm-5.2 ≠ requested deepseek-v4-flash）——F4 事故成为机器可检项。
- **M1（插件源码仓骨架）已完成**：`dsh-plugins/uniflow-agent-loop/`
  （package.json files:src / peerDeps / dsh.client.inject；纯函数 gates.js
  ——envelope 形状 + requested-vs-actual 回执核对，PENDING→RECEIPT_LOST 映射；
  index.js 会话级 envelope/receipt 登记 + dispose 清理）。11 用例全绿
  （node:test 零依赖）；`npm pack --dry-run` 验证自包含（6 文件，无依赖泄漏）。
- **M2–M5 已完成（2026-08-25 晚）**：
  - M2 `provider.js`：`registerProvider('uniflow')` 装饰 spawn provider——
    无合法 envelope 拒绝启动（WORK_ITEM_REQUIRED）、路由缺失在子 Agent 创建前
    fail-closed（ROUTING_CAPABILITY_LIMIT）、agentOptions 的 provider/model 被
    绑定强制覆盖、spawn 失败回队列可重试；
  - M3/M4 `index.js`：根级 `agent/request` waterfall——注入 reasoningEffort
    并从实际 LlmCallConfig 捕获回执（机器真相）；服务经 `ctx.reflect.provide`
    注册（E2E 实测纠正 `ctx.plugin` 误用）；
  - M5 E2E 流程测试全绿（`test/e2e/run-e2e.sh`，五步：unit 23/23 →
    DISPATCH_OK → E2E_PASS → worker session 实际模型确认 → RECEIPT_OK）。
    E2E 修掉四个纸面发现不了的真实缺陷（overlay insert 语义、cordis 服务
    注册 API、record 路径、BSD find 在 `--` 目录下的 -name 失效）。
- **M6 已完成（2026-08-25 深夜）**：版本 1.0.0；`files` 补齐 4 源文件 +
  exports 全暴露；README 状态表 + E2E 章节 + 配置语义纠正（config.profileSource
  为插件行配置，解析失败降级 health，绑定真相在 envelope 登记链）；
  `npm run e2e` 脚本入口；uni_claw 侧 `check_install_integrity` 入
  `validate`（悬空 file: 依赖 + 悬空符号链接机检，5 用例 + 4 场景验证）；
  发布 tarball 已产出（dsh-uniflow-agent-loop-1.0.0.tgz，9 文件 11KB）。
  **npm publish 需人工登录执行**（ENEEDAUTH；包名公共 scope）。
  repo 侧最终回归：121/121 + validator PASS + consistency ALL PASS +
  git diff --check PASS。