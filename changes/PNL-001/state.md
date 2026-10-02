# PNL-001 — UniClaw Task Workbench（DSH client 插件，第一片）

lifecycle_state: implemented · disposition: none · depth: decision-heavy · base: 7b49038d (dirty worktree)

## Intent（WHAT/WHY）

Owner 在 DSH GUI 里缺少任务维度的管控面：Product 咨询、需求发起的任务对话、
trace、诊断技能散落在普通会话列表里，「启动了什么、跑过什么、执行情况」
不可见。本 change 建立 **Task Workbench**：以 UniClaw Task（任务定义）/
Task Instance（任务实例）为核心的 DSH client 插件，往最终形态做、按小切片
交付。术语见 CONTEXT.md §Task Workbench。

## 已锁定决策（grill 2026-09-30，Q1–Q15）

1. **Task = 任务定义，Session = 任务实例**：一个 Task 可实例化多次；
   实例身份 = productSessionId。v1 以 realization 投影落地（插件私有元
   数据），不动 CLOSED 的产品基线；元数据形状经真实使用验证后一次 ADR
   升格（ADR-0026 缝验证原则）。
2. **新插件** `@uniclaw/dsh-task-workbench`，不塞 decision-channel
   （决策缝职责冻结于 AGT-001/002）。
3. **存储走 Repository 缝**：v1 JSON 文件（默认
   `~/.dsh/uniclaw-tasks/tasks.json`），后续入库只换实现不改调用方。
4. **Task Project = DSH workspace**：任务归类落为 workspace（与
   workspaceRegistry.list() 同源，零第二真相）。
5. **发起 = 特定接口 + 数据协议**，手动/自动只是不同调用方：
   authenticated HTTP `POST /api/uniclaw-task/*`，协议为版本化 JSON
   Schema（schema/ 目录 + hash 自校验，同 product-protocol 模式）。
6. **任务 session 用新 preset `uniagent-task`**：allow-list 收窄工具面
   （读 + bash + skill 加载；name 级白名单，晚注册的 MCP 工具默认不进）。
7. **对话展示 = 全量事件流**（工程师调试视图），按分类酌情多 tab。
8. **trace = 物理（sessionQuery.listEvents 时间线）+ 语义（UniFlow
   gate/outcome + Evidence Ledger 引用）**；v1 语义 trace 只定数据结构
   并留空 tab，数据源等产品缝成型再接（不预造产品缝）。
9. **诊断技能 = 注入一轮 prompt**（加载 diagnosing-bugs 等skill 在任务
   session 内执行，上下文连续）。
10. **Task 定义最小字段**：taskId/title/requirement/projectRef/status/
    createdAt（+updatedAt）；实例：instanceId(=productSessionId)/
    sessionId/status/startedAt(/endedAt)。不加 priority/tags。
11. **PNL-001 吸收原「管控面板」方案**：已写的 decision-channel 事件
    台账保留（Product 咨询监管）；其 client 半部骨架迁移为 workbench
    外壳，Product 咨询监管降为其中一节。

## Scope（切片）

- **S1 骨架+存储+协议**：插件包、TaskRepository(JSON)、task 定义 CRUD、
  instantiate HTTP route、task-protocol schema + hash。
- **S2 实例化**：uniagent-task preset（profile 声明行 + allow-list 组合
  插件）、instantiate 建 session（sessionController/workspaceRegistry）、
  实例关联记录。
- **S3 Client 半部**：任务列表、创建（需求文本+workspace 选择）、任务
  详情（对话=全量事件流，分类 tab）。
- **S4 trace + 诊断**：物理 trace tab（listEvents）、语义 trace 占位
  数据结构、诊断技能按钮（prompt 注入）。
- **S5 部署面**：deploy.sh 增加第三个包、preset 行部署说明、
  decision-channel 面板骨架迁移处置。

## Out of Scope

- 语义 trace 的产品缝（Evidence Ledger 投影、UniFlow outcome 事件化）。
- Task 升格进产品基线（后续 ADR）；priority/tags/assignee 等字段。
- 自动发起的元数据来源（v1 只保证入口协议统一）。
- 命令级 bash 白名单（DSH restrict 是 name 级；v1 接受工具级收窄）。

## Acceptance

1. HTTP 协议按 schema 校验失败 fail-closed；task/instance CRUD 与 JSON
   持久化经 node --test 证明（mock ctx，不碰真实 profile）。
2. instantiate 在 mock controller 下创建 session 并记录实例映射。
3. client 半部 bundle 冒烟（假 __ModuleLoader__）通过。
4. 部署后 GUI 出现任务面板（S3 起，需 Owner 重启实例实测）。

## Verification

```yaml
level: CONTRACT
method: cd dsh/uniclaw-task-workbench && npm test；node --check 两入口
expected: 全部用例通过
actual: 终验全绿——task-workbench 21/21（host 协议 9 + panel 服务 7 + client 冒烟 5）、
task-scope 7/7、decision-channel 21/21（client 回收后）；node --check 全部入口通过；
deploy.sh bash -n 通过、--check-only 覆盖四包、HARD RULE 未变；git 越界检查干净
evidence: dsh/uniclaw-task-workbench/tests/workbench.test.mjs
```

## Status log

- 2026-09-30 · understanding→resolved · grill Q1–Q15 全部闭合（见上）。
- 2026-09-30 · resolved→persisted · 本 state.md；CONTEXT.md 增 Task
  Workbench 术语节。
- 2026-09-30 · persisted→planned · 切片 S1–S5 定稿。
- 2026-09-30 · planned→implemented · S1+S2 经 Leader→GLM-5.3-Flash 双 WorkItem
  （WI-PNL001-001/002）并行派发完成；Leader 复验通过（见 verification）。
  Leader 决策：schema-hash 采用原始字节 sha256（改 schema 后一条命令重出，
  非 canonical-JSON）；test script 用 tests/*.test.mjs glob 形式（目录形式
  在本环境对冻结包同样失败，环境 quirk）。
- 2026-09-30 · implemented · S3+S4 完成（WI-PNL001-003/004 并行）：host 侧
  uniclawTaskPanel typert 六方法服务（instantiate 与 HTTP 共用内核；语义
  trace 空占位；诊断 skill 冻结枚举）+ client 半部（任务列表/新建/详情三
  tab：对话分类子tab/Trace/操作）；并行竞态致 wi004 曾见 2 用例红，合并后
  Leader 复验 21/21。
- 2026-09-30 · implemented · S5 完成（WI-PNL001-005）：deploy.sh 扩四包
  （marker/diff/usage 同步）；decision-channel client 半部按决策 11 回收
  （删 client.js/导出/冒烟用例，回纯 host 形态，ledger 与 uniclawDecisionPanel
  服务保留）；dsh/README 四包化 + preset 手动部署提示。
- 2026-09-30 · implemented→hold-live-verify · 全部 CONTRACT 级 acceptance 已
  证明；S3 A4（GUI 实测）需 Owner 部署：deploy.sh 真跑 + uniagent-task
  preset 行写入 profile cordis.patch.yml + 重启实例 + 强刷。此为人工 Gate，
  change 保持 open。
- 2026-09-30 · hold-live-verify（部署已执行）· 四包部署到 web profile
  （markers OK / drift clean 22 文件）；preset-uniclaw-task 行写入
  cordis.patch.yml（allow=read,glob,grep,bash,skill，逐名对 live catalog
  核对）。部署中修了 deploy.sh 两个缺陷（首装 remove 必败；pipefail×
  grep -q SIGPIPE 假阴性——后者是本 change 撑大文件后显形的潜伏 bug）。
  证据：evidence/PNL-001-deploy.md。仅剩 Owner：重启 3080 + 强刷实测。
- 2026-09-30 · live 深夜轮 · ① 发现并修复 typert API 三处平台漂移（codec
  create 工厂 / 参数 descriptor 对象 / host 位置收参），技能 recipe 过期是根因；
  ② task-workbench 需 profile patch 挂载行（已补）；③ shell cookie 铸造后全
  链路实证：createTask/overview/落盘 PASS（evidence/PNL-001-deploy.md）；
  ④ ego 浏览器渲染层整体卡死，GUI 点击流留给 Owner 强刷验证；⑤ uniagent-task
  preset 收窄机制平台不可行已回滚——架构裁决项，见 Gate。

## 方向修正（Owner 2026-09-30 深夜，Q16）

对话 = 任务实例；对话列表/对话视图复用 DSH 原生 UI（不建第二套）；任务定义
不展示（createTask/instantiate 保留为程序化 API）。插件自建 UI 收敛为唯一的
**UniClaw trace 视图**：按 session 展示语义 trace（UniFlow gate/outcome
token + Evidence 引用），数据经 sessionQuery 从事件真实提取。

## Status log 补充

- 2026-09-30 · Q16 落地 · WI-006/007（GLM-5.3-Flash）重塑 host/client；
  Leader 修 sessionQuery 真实形状（SessionRecord.header + readTitleSnapshots +
  readSession 事件源）。live：sessions/trace/createTask 全链路实证（见
  evidence）。GUI 视觉验证留 Owner（ego 浏览器渲染层卡死，不代重启）。

## Residual risks / Gate

- uniagent-task preset 行需写入 Owner 的 profile cordis.patch.yml（部署
  动作，S5）；部署前 instantiate 真实调用会失败（测试用 mock 覆盖）。
- 60s 测试悬挂为 AGT-003 dirty 基线既有现象，另行立项，不在本 change。
