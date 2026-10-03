# PNL-002 — UniClaw Product Workspace（DSH client vertical slice）

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 7b49038d

## Intent（WHAT/WHY）

把 DSH 里的 UniClaw 能力从底部 Trace 弹层提升为可切换进入的产品工作区。一个
Task Instance 对应一个 DSH Session；用户在同一实例上下文里查看执行对话、DSH
物理轨迹、UniClaw 语义 trace、元数据和关联证据。

## Scope

- client：新增全屏 UniClaw Workspace 入口与三栏布局；左侧按项目分组任务实例，
  中间展示会话事件流，右侧切换 DSH Trace / UniClaw Trace / Metadata / Evidence。
- host：提供 workspace 实例索引和 session detail 投影；所有字段来自任务仓库或
  `sessionQuery`，不伪造没有证据的数据。
- protocol：扩展现有 Typert panel 的只读 `workspace` / `session` / `artifact` 方法；保留
  既有 HTTP task/instantiate API 和兼容的 `sessions` / `trace` 方法。
- verification：Node 合同测试、client bundle smoke、真实 session 读取形状测试；
  GUI 重启/强刷留作环境级证据。

## Out of Scope

- 不修改 UniClaw Product Runtime authority、Run/World/Evidence 语义或 DSH 原生
  会话存储。
- 不引入第二份对话、trace 或证据真相；workspace 只做只读 projection。
- 不在本切片增加任务编辑器、筛选器、搜索、实时 websocket 或新的持久化层。

## Decisions

1. 最新 Owner 指令覆盖 PNL-001 Q16 的“只展示 UniClaw Trace”方向；本 change
   以任务实例为工作区主导航，DSH 原生界面仍保留为宿主入口。
2. 左侧只列任务实例；没有实例的任务定义不进入实例列表，避免把 Task 定义误称
   为执行过程。
3. DSH Trace = `sessionQuery.readSession().events` 的物理事件投影；UniClaw
   Trace = 对话中可识别的 UniFlow gate/outcome/evidence 语义投影，并单独接入
   已明确关联的 `trc/0.1` Kernel RunTrace；Evidence 展示事件引用与同一运行目录
   的已关联产物文件。
4. Metadata 只投影 session header、task、instance 和 projectRef 中的已有字段；
   不从模型输出推断 Product 状态。
5. 调用方咨询请求从真实消息的 `AgentDecisionContext (JSON)` 提取为请求卡片；
   禁止整段隐藏而丢失目标与上下文。决策由 `submit_decision` 工具调用投影，按
   callId 关联提交结果；提交接受不等于 Product 动作执行成功。原始安全内容可展开。

## Acceptance

1. 点击 DSH 侧栏的 UniClaw 入口可进入/退出全屏工作区，且不修改 DSH 原生会话。
2. 左侧按项目显示完整 Task Instance 列表；点击实例后中间出现其会话事件流。
3. 右侧可切换 DSH Trace、UniClaw Trace、Metadata、Evidence 四类信息，缺数据时
   显示空态而不生成占位事实。
4. UniClaw Trace 与 Evidence 每条可读记录都有“查看明细”入口；Trace 展开结构化
   条目，Evidence 按当前 session 读取已关联运行产物，未关联引用失败关闭。
5. Host/client 契约测试通过，旧 task/instantiate API 保持可用。
6. 咨询目标/phase/页面/预算可见；决策类型/理由/步骤与提交结果可读；重复的
   assistant tool-call 与 tool/call 只展示一次，关联必须来自真实 callId。
7. 中心区按咨询回合展示“请求 → 决策 → 提交”分组；明确关联运行产物后追加独立的
   “执行结果 → 验证结果”阶段；没有证据时不填充成功状态，扁平 `conversation` 作为
   兼容投影保留。

## Verification

```yaml
level: CONTRACT+DETERMINISTIC
method: node --test dsh/uniclaw-task-workbench/tests/*.test.mjs; node --check client/host; git diff --check
expected: contract and projection tests green; no unrelated dirty files changed
actual: 31/31 workbench tests green; host/client/artifact entrypoints pass `node --check`; `git diff --check` clean. Artifact contract verifies absolute and session-associated relative reads, while unassociated refs return `artifact-not-found`. Dedicated 3083 DSH acceptance shows the 证据 tab with “查看明细”, reads `facts.json` into an in-panel detail block, and keeps other evidence refs actionable; the UniClaw Trace client path exposes the same detail action and caps rendered Kernel spans at 200 to keep the pane responsive.
evidence: dsh/uniclaw-task-workbench/tests/workbench.test.mjs; dsh/uniclaw-task-workbench/tests/client.test.mjs; dsh/deploy.sh output
```

## Status log

- 2026-10-02 · verified · 修复请求被整段过滤的问题；提取请求与决策卡片，并在
  同一真实 Session 验证 3 次咨询请求重新可见，3 张 submit_decision 决策卡按真实
  callId 关联提交结果，重复 assistant/tool-call 合并、原文可展开。
- 2026-10-02 · implemented · 将扁平 Uni-Agent cards 增加 `conversationGroups` 回合
  投影；Client 改为按请求 → 决策 → 提交渲染，保留扁平投影兼容旧调用方。
- 2026-10-02 · implemented · 将提交回执固定为回合内的 `submission` 阶段，并从明确
  关联的 `facts.json` / `coverage-steps.json` / UniFlow `VERIFIED` 证据投影独立的
  `runStages`；没有执行或验证证据时，界面只显示说明，不冒充成功。
- 2026-10-02 · implemented · 为 UniClaw Trace 与 Evidence 增加“查看明细”入口；Trace
  在客户端展开结构化条目，Evidence 通过宿主 `artifact({sessionId, ref})` 按需读取
  已关联运行产物，并对未关联引用失败关闭。

- 2026-10-01 · understood→planned · 真实 DSH 页面确认现有插件只是 Trace 弹层；Owner
  明确要求以 Task Instance 为主导航的独立 UniClaw 工作区。
- 2026-10-01 · planned→implemented · client 改为可切换全屏三栏工作区；host 增加
  `workspace/session` 只读 projection；显式 `dshSessionId` 运行产物接入 Kernel
  RunTrace 与 evidence；29/29 合同测试通过。
- 2026-10-01 · implemented · profile snapshot 已重新部署并通过 23-file drift check；
  3081 dedicated service 可注册新 panel，但浏览器通知权限提示阻断了自动 GUI 复核。
- 2026-10-01 · verified · 3082 实际页面打开工作区、读取实例、切换 DSH Trace / UniClaw
  Trace / 元数据 / 证据均通过；修复首次自动选中实例触发的重复 remote mount。
- 2026-10-01 · verified · 3083 实际页面复核 Uni-Agent 主线：隐藏 runtime prompt/context，中心
  只保留 Uni-Agent、能力调用、能力结果和回合结束事件；右侧 DSH Trace 仍可查看底层事件。

## Closure verification（2026-10-03）

PNL-002 的只读 Product Workspace vertical slice 已完成并由 PNL-003 的共享前端
与 DSH browser bridge 继续验证。项目分组、任务实例到 session 的映射、请求→决策→
提交对话、DSH/UniClaw Trace、Evidence 明细、Metadata 和执行结果均在同一真实
Android Settings session 上可达；未关联、无产物和局部失败仍按结构化状态显示。

| method | expected | actual | evidence |
|---|---|---|---|
| DSH contract regression | task/workspace/session/artifact capability 与旧 API 兼容 | DSH package **50/50**；`node --check` 与 `git diff --check` 通过 | `dsh/uniclaw-task-workbench/tests/*.test.mjs`；`evidence/PNL-003-WI-PNL003-016.md` |
| shared UI regression | Workspace renderer、view-model、controller 不回归 | Web package **66/66**；browser bundle 生成 7 个 shared modules | `web/uniclaw-workspace/tests/`；bundle build output |
| live scenario | 真实任务可按项目进入并读取完整只读投影 | 专用 DSH `3083` 上真实 Android API 35 emulator 任务完成 3 轮对话；DSH Trace 50、UniClaw Trace 200、Evidence 5 个明细入口、设备 Metadata 与 Completion 可读 | `evidence/PNL-003-WI-PNL003-016.md`；真实浏览器 DOM/截图 |
| boundary | Workspace 不接管 DSH session/artifact 权威 | DSH 负责 session/Host trace，UniClaw 负责 ProductSession/Runtime 语义，Workspace 只读组合；未关联事实不升级为 Product truth | 本文件 Decisions；`docs/reports/PNL-003-observability-project-management-gap-report.md` |

- 2026-10-03 · VERIFY → CLOSED · PNL-002 vertical slice 的契约、双入口共享
  前端和真实 3083 场景均有证据；后续 Trace 完整性、Metadata 合约、Project/TestSet
  catalog 作为独立下一轮 Change 输入，不阻塞本切片关闭。
