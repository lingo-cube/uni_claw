# `@uniclaw/dsh-task-workbench`

UniClaw Product Workspace 的 host 半部（PNL-002）。它保留 Task 定义和 Task
Instance 的 HTTP API，并提供只读 Typert 投影给静态 Client：左侧是按项目分组的
任务实例，中间默认是以“调用方请求 → Uni-Agent 决策 → 提交结果”为主线的解决过程，
可独立切换 Trace；证据、任务概览、工具及选中记录通过右侧详情面板按需打开。

## 安装

插件随 DSH web profile 的宿主插件行挂载（源码无需构建步骤）：

```yaml
- id: uniclaw-task-workbench
  name: '@uniclaw/dsh-task-workbench'
```

JSON 存储默认在 `~/.dsh/uniclaw-tasks/tasks.json`（config `storePath` 可覆盖）。
协议自校验读取 `schema/` 下的 `task-protocol.schema.json` 与 `schema-hash.txt`；
二者不一致时插件拒绝启动。运行产物默认从当前 workspace 的 `evidence/` 读取，
也可以通过 `artifactRoots` 配置覆盖。

本地测试集目录由 Host 通过 `UNICLAW_TESTSET_ROOT` 配置；它指向仓库外置的
`testsets/` 根目录，避免把测试资产复制进 DSH 安装快照。`dsh/test-service.sh`
会默认将该变量指向当前仓库的 `testsets/`，生产 Host 应按自己的本地存储布局设置。

## Panel 方法

| 方法 | 作用 |
|---|---|
| `workspace()` | 项目分组的 Task Instance 导航；带有明确 `dshSessionId` 的运行产物也会被纳入 |
| `session({sessionId})` | 单实例详情：`conversationGroups`（请求 → 决策 → 提交）、可选 `runStages`（执行结果 → 验证结果）、兼容的 `conversation` cards、DSH 物理事件、Kernel RunTrace、UniFlow 语义投影、metadata、evidence |
| `artifact({sessionId, ref})` | 读取当前 session 已关联运行产物的截断文本；未关联的相对路径或文件会失败关闭 |
| `sessions()` | 兼容的 DSH session 列表与标题投影 |
| `trace({sessionId})` | 兼容的 UniFlow gate/outcome/evidence 事件投影 |
| `launch({TaskLaunchRequest})` | 经 `uniclawRuntime` Host adapter 创建/恢复 Runtime Run 和 Product Session；不在 DSH 侧生成 identity |

### Runtime launch transport

插件启动时通过 DSH 的 `ctx.reflect.provide('uniclawRuntime', provider)` 注册
Host-facing Runtime adapter；如果宿主已经提供同名 provider，则保留宿主实现。
默认 adapter 只调用外部 Runtime，不在 DSH 侧生成 `runId` 或
`productSessionId`。配置 `UNICLAW_RUNTIME_BASE_URL`（或插件 `runtimeBaseUrl`）
后使用以下接口：

- `POST /api/uniclaw-runtime/runs`：接收版本化逻辑引用和幂等字段，必须返回
  `runId` 与 `productSessionId`。
- `POST /api/uniclaw-runtime/runs/recover`：按 `launchId` 与
  `idempotencyKey` 恢复同一运行，也必须返回这两个字段。

未配置 endpoint、transport 超时或响应缺少 Runtime-owned identity 时，provider
返回结构化 `runtime-unavailable` / `runtime-response-invalid`，不会生成 fallback
ID。当前仓库的 `UniClaw.Host.Dsh` 仍是 console composition root；接入真实运行前，
需要由 Host/Runtime 侧提供上述 transport。

`conversationGroups` 从真实 `AgentDecisionContext (JSON)`、`submit_decision` call/result
和回合事件构造，每个回合按请求 → 决策 → 提交归组；提交结果只表示决策已被接收，
不等同于 Product 动作执行成功。与明确关联运行产物中的 `metadata.outcome` / `facts.json`
和完成锚点相关的 `runStages` 才会追加“执行结果 → 验证结果”；没有证据时只显示说明，
不填充成功状态。兼容的 `conversation` 保留扁平 cards。DSH runtime
prompt/context 不进入中心主线。`session()` 的 Kernel Trace 只接受 `metadata.json.dshSessionId` 与同一运行目录下的
`trc/0.1` `trace.json`。不具备明确关联的文件会被忽略。对话里的 UniFlow 词条
单独放入 `uniflowTrace`，不会冒充 Product Trace。
UniClaw Trace 每条语义记录都提供“查看明细”按钮，在工作区内展开结构化条目；证据引用提供同样的入口，按需读取关联文件内容，避免只显示链接或相对文件名。

决策卡的正文只展示决策自身内容或理由，不使用嵌套的提交回执作后备正文；提交回执
仍在单独的“提交结果”卡显示。DSH 浏览器 adapter 会合并同一产品会话/宿主会话的
并发详情读取，让会话、对话、Trace 和 Evidence 共用该次返回的内容与读取标记。
读取完成或失败后立即释放在途记录，下一次刷新重新查询；这不构成长期缓存，也不
声明跨 Runtime/Host 来源的原子快照。

## 工作台布局与阅读

- 默认以解决过程为主区，Trace 独立切换；Metadata 和执行概览放在“概览”中。
- 点击对话的“查看依据/查看记录”展示该条已有数据；Trace 节点、Evidence 的详情沿用原有只读接口，不新增执行命令。
- 桌面详情与主区并排；较窄窗口使用可关闭的详情抽屉。手机任务导航也使用抽屉，Escape 可关闭并返回触发入口；详情抽屉限制焦点在其内部。
- 旧版结构化步骤优先显示来源标签，否则显示动作与目标；已知 tap 显示“点击”，未知动作保留原值，期望状态单独标注。每个对象步骤均可展开完整参数，不据提议步骤推断执行成功。
- 对话减少重复卡片边框，以轻量图标区分角色；工具回执使用摘要行和原生可展开详情，结构化/长输出折叠，错误正文始终直接可见。工具状态只显示已有来源字段，不据回执推断设备执行成功。
- 工具目录使用窄面板卡片，展开查看调用形态与姿态；此目录需要 Host 原有 ToolInvoke 能力，未提供时继续显示不可用，不新增 DSH 调用权限。
- 页面重新挂载时按任务保留滚动锚点、Trace/工具详情展开状态和控件焦点。阅读旧内容时显示“有新内容”，只有靠近底部时自动跟随。
- 阅读状态只驻留当前 UI 生命周期，不持久复制 Host/Runtime 数据。新面板与主视图状态不进入 Product 协议。

## 验证

```bash
cd dsh/uniclaw-task-workbench && npm test
node --check src/index.js
node --check src/client.js
node --check src/runtime-provider.js
```

路由（均在 authenticated `/api` lane）：

| 路由 | 作用 |
|---|---|
| `GET /api/uniclaw-task/tasks` | 列出任务定义（含实例） |
| `POST /api/uniclaw-task/tasks` | 创建任务定义（title/requirement 必填） |
| `POST /api/uniclaw-task/tasks/instantiate` | 实例化：active 任务 → uniagent-task session |
| `POST /api/uniclaw-task/tasks/launch` | TaskLaunchRequest → Runtime Run/Product Session/Host Session 绑定 |

## 回滚

1. 从 profile 的宿主插件行移除 `uniclaw-task-workbench` 一行并重启 DSH，路由和
   工作空间入口即消失；`uniagent-task` preset 行如已加入也一并移除。
2. 数据回滚：删除 `~/.dsh/uniclaw-tasks/tasks.json`（或使用 `storePath` 指向的
   文件）。文件形状带 `storeVersion`，不兼容版本会 fail-closed。
3. 插件不写 DSH session/workspace storage；它只读取 sessionQuery 和已关联运行
   产物，Task Instance 记录仍由 JSON repository 管理。
